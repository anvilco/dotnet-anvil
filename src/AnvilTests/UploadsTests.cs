using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Anvil;
using Anvil.Payloads;

using AnvilTests.Support;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    public class UploadsTests : IDisposable
    {
        private static readonly (string, string)[] Limits = { ("X-RateLimit-Limit", "100"), ("X-RateLimit-Interval-Ms", "1000") };
        private const string Mutation = "mutation Upload($file: Upload!) { createCast(file: $file, isTemplate: true) { eid } }";
        private const string Response = @"{""data"":{""createCast"":{""eid"":""cast1""}}}";
        private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 test file");

        private readonly string _pdfPath = Path.Combine(Path.GetTempPath(), $"anvil-upload-{Guid.NewGuid():N}.pdf");

        public UploadsTests()
        {
            File.WriteAllBytes(_pdfPath, PdfBytes);
        }

        public void Dispose()
        {
            File.Delete(_pdfPath);
        }

        /// <summary>A part of a multipart body: its headers and content.</summary>
        private sealed class Part
        {
            public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Content { get; set; } = "";
            public string Name => Regex.Match(Headers["Content-Disposition"], "name=\"([^\"]*)\"").Groups[1].Value;
            public string? Filename => Headers["Content-Disposition"].Contains("filename=") ? Regex.Match(Headers["Content-Disposition"], "filename=\"([^\"]*)\"").Groups[1].Value : null;
        }

        private static List<Part> Parts(RecordedRequest request)
        {
            var boundary = Regex.Match(request.ContentType!, "boundary=\"?([^\";]+)\"?").Groups[1].Value;
            var parts = new List<Part>();
            foreach (var raw in request.Body!.Split(new[] { "--" + boundary }, StringSplitOptions.None).Skip(1))
            {
                if (raw.StartsWith("--"))
                {
                    break;
                }

                var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                var part = new Part { Content = raw.Substring(split + 4).TrimEnd('\r', '\n') };
                foreach (var line in raw.Substring(0, split).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = line.IndexOf(':');
                    part.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
                }

                parts.Add(part);
            }

            return parts;
        }

        [Fact]
        public async Task AFilePath_IsSentAsMultipart()
        {
            var handler = new FakeHandler().RespondJson(Response, Limits);
            using var client = Clients.Create(handler);

            var data = await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(_pdfPath) });

            Assert.Equal("cast1", data["createCast"]!["eid"]!.ToString());
            Assert.StartsWith("multipart/form-data", handler.LastRequest.ContentType);
            var parts = Parts(handler.LastRequest);
            Assert.Equal(new[] { "operations", "map", "1" }, parts.Select(p => p.Name));

            var operations = JObject.Parse(parts[0].Content);
            Assert.Equal(Mutation, operations["query"]!.ToString());
            Assert.Equal(JTokenType.Null, operations["variables"]!["file"]!.Type);
            Assert.Equal(@"{""1"":[""variables.file""]}", parts[1].Content);
            Assert.Equal(Path.GetFileName(_pdfPath), parts[2].Filename);
            Assert.Equal("application/pdf", parts[2].Headers["Content-Type"]);
            Assert.Equal("%PDF-1.4 test file", parts[2].Content);
        }

        [Fact]
        public async Task BytesAndStreams_AreSentWithTheirFilenameAndMimetype()
        {
            var handler = new FakeHandler().RespondJson(Response, Limits).RespondJson(Response, Limits).RespondJson(Response, Limits);
            using var client = Clients.Create(handler);

            await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(PdfBytes, "from-bytes.pdf") });
            var part = Parts(handler.LastRequest)[2];
            Assert.Equal("from-bytes.pdf", part.Filename);
            Assert.Equal("application/pdf", part.Headers["Content-Type"]);
            Assert.Equal("%PDF-1.4 test file", part.Content);

            await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(new MemoryStream(PdfBytes), "from-stream.png", "image/png") });
            part = Parts(handler.LastRequest)[2];
            Assert.Equal("from-stream.png", part.Filename);
            Assert.Equal("image/png", part.Headers["Content-Type"]);

            using var file = File.OpenRead(_pdfPath);
            await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(file) });
            Assert.Equal(Path.GetFileName(_pdfPath), Parts(handler.LastRequest)[2].Filename);
        }

        [Fact]
        public void AFilenameIsRequired_ForBytesAndNamelessStreams()
        {
            Assert.Throws<ArgumentException>(() => AnvilClient.PrepareGraphQLFile(PdfBytes, ""));
            Assert.Throws<ArgumentException>(() => AnvilClient.PrepareGraphQLFile(new MemoryStream()));
            Assert.Equal("application/octet-stream", AnvilClient.PrepareGraphQLFile(PdfBytes, "unknown.bin").Mimetype);
        }

        [Fact]
        public async Task NestedFiles_AreMappedByPath_AndTheSameFileOnce()
        {
            var handler = new FakeHandler().RespondJson(@"{""data"":{""createEtchPacket"":{""eid"":""p""}}}", Limits);
            using var client = Clients.Create(handler);
            var upload = AnvilClient.PrepareGraphQLFile(_pdfPath);

            await client.CreateEtchPacketAsync(new CreateEtchPacketPayload
            {
                Name = "Uploads",
                Files = new List<Anvil.Types.GraphQL.EtchFile>
                {
                    new DocumentUpload { Id = "a", File = upload },
                    new EtchCastRef { Id = "b", CastEid = "cast1" },
                    new DocumentUpload { Id = "c", File = AnvilClient.PrepareGraphQLFile(PdfBytes, "second.pdf") },
                    new DocumentUpload { Id = "d", File = upload },
                },
            });

            var parts = Parts(handler.LastRequest);
            Assert.Equal(new[] { "operations", "map", "1", "2" }, parts.Select(p => p.Name));
            var map = JObject.Parse(parts[1].Content);
            Assert.Equal(new[] { "variables.files.0.file", "variables.files.3.file" }, map["1"]!.Select(p => p.ToString()));
            Assert.Equal(new[] { "variables.files.2.file" }, map["2"]!.Select(p => p.ToString()));
            var files = (JArray)JObject.Parse(parts[0].Content)["variables"]!["files"]!;
            Assert.Equal(JTokenType.Null, files[0]["file"]!.Type);
            Assert.Equal("cast1", files[1]["castEid"]!.ToString());
            Assert.Equal("second.pdf", parts[3].Filename);
        }

        [Fact]
        public async Task Base64AndUrlFiles_StayJson()
        {
            var handler = new FakeHandler().RespondJson(Response, Limits).RespondJson(Response, Limits);
            using var client = Clients.Create(handler);

            await client.RequestGraphQLAsync(Mutation, new { file = new Base64Upload { Data = "JVBERi0=", Filename = "doc.pdf" } });
            Assert.StartsWith("application/json", handler.LastRequest.ContentType);
            Assert.Equal("JVBERi0=", JObject.Parse(handler.LastRequest.Body!)["variables"]!["file"]!["data"]!.ToString());

            await client.RequestGraphQLAsync(Mutation, new { file = new UrlUpload { Url = "https://example.com/doc.pdf", Filename = "doc.pdf" } });
            Assert.StartsWith("application/json", handler.LastRequest.ContentType);
            var file = JObject.Parse(handler.LastRequest.Body!)["variables"]!["file"]!;
            Assert.Equal("https://example.com/doc.pdf", file["url"]!.ToString());
            Assert.Equal("doc.pdf", file["filename"]!.ToString());
        }

        [Fact]
        public async Task AFileUnderAnotherKey_IsInvalid()
        {
            using var client = Clients.Create(new FakeHandler().RespondJson(Response, Limits));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestGraphQLAsync(Mutation, new { document = AnvilClient.PrepareGraphQLFile(_pdfPath) }));

            Assert.Contains("Invalid File schema detected", ex.Message);
            Assert.Contains("`document`", ex.Message);
        }

        [Fact]
        public async Task ARawStringOrIncompleteBase64Object_IsInvalid()
        {
            using var client = Clients.Create(new FakeHandler().RespondJson(Response, Limits));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestGraphQLAsync(Mutation, new { file = _pdfPath }));
            Assert.Contains("PrepareGraphQLFile", ex.Message);

            ex = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestGraphQLAsync(Mutation, new { file = "https://example.com/doc.pdf" }));
            Assert.Contains("UrlUpload", ex.Message);

            ex = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestGraphQLAsync(Mutation, new { file = new { data = "JVBERi0=" } }));
            Assert.Contains("filename and mimetype", ex.Message);

            ex = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestGraphQLAsync(Mutation, new { file = new { something = "else" } }));
            Assert.Contains("Invalid File schema detected", ex.Message);
        }

        [Fact]
        public async Task AnUpload_IsSentAgainAfterA429()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .RespondJson(Response, Limits);
            using var client = Clients.Create(handler);

            await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(_pdfPath) });

            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal("%PDF-1.4 test file", Parts(handler.Requests[0])[2].Content);
            Assert.Equal("%PDF-1.4 test file", Parts(handler.Requests[1])[2].Content);
        }

        [Fact]
        public async Task ASeekableStream_IsRewoundForTheRetry_ANonSeekableOneFails()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .RespondJson(Response, Limits);
            using var client = Clients.Create(handler);

            await client.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(new MemoryStream(PdfBytes), "doc.pdf") });
            Assert.Equal("%PDF-1.4 test file", Parts(handler.Requests[1])[2].Content);

            var oneShot = new FakeHandler().Respond(429, "", "text/plain", ("Retry-After", "0")).RespondJson(Response, Limits);
            using var client2 = Clients.Create(oneShot);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client2.RequestGraphQLAsync(Mutation, new { file = AnvilClient.PrepareGraphQLFile(new NonSeekableStream(PdfBytes), "doc.pdf") }));
            Assert.Contains("Can't send doc.pdf again", ex.Message);
        }

        [Fact]
        public async Task TheCallersStream_IsNotDisposed()
        {
            var stream = new MemoryStream(PdfBytes);
            var upload = AnvilClient.PrepareGraphQLFile(stream, "doc.pdf");

            using (var content = upload.OpenContent())
            {
                await content.ReadAsByteArrayAsync();
            }

            Assert.True(stream.CanRead);
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] bytes) : base(bytes)
            {
            }

            public override bool CanSeek => false;
        }
    }
}
