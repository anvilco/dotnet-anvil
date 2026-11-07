using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Anvil;
using Anvil.Payloads;

using AnvilTests.Support;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    /// <summary>
    /// The client against a real HTTP server on a loopback port, so what goes on
    /// the wire is checked: headers, bodies, multipart parts, rate limit timing,
    /// connection failures and timeouts.
    /// </summary>
    public class HttpTests : IDisposable
    {
        private static readonly (string, string)[] Limits = { ("X-RateLimit-Limit", "100"), ("X-RateLimit-Interval-Ms", "1000") };
        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 on the wire");

        private readonly LoopbackServer _server = LoopbackServer.Start();

        public void Dispose() => _server.Dispose();

        private AnvilClient Client(Action<AnvilClientOptions>? configure = null)
        {
            return new AnvilClient(Apply(new AnvilClientOptions
            {
                ApiKey = "key-" + Guid.NewGuid().ToString("N"),
                BaseUrl = _server.Url,
                MaxRetries = 0,
            }, configure));
        }

        private static AnvilClientOptions Apply(AnvilClientOptions options, Action<AnvilClientOptions>? configure)
        {
            configure?.Invoke(options);
            return options;
        }

        [Fact]
        public async Task Requests_CarryTheHeaders()
        {
            _server.RespondJson("{}", Limits);
            using var client = Client(o => o.ApiKey = "my-api-key");

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            var request = _server.LastRequest;
            Assert.Equal($"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes("my-api-key:"))}", request.Headers["Authorization"]);
            Assert.Equal($"dotnet-anvil/{AnvilClient.Version}", request.Headers["User-Agent"]);
            Assert.Equal("application/json", request.Headers["Accept"]);
        }

        [Fact]
        public async Task Requests_GoToTheRightPaths()
        {
            _server.RespondJson(@"{""data"":{}}", Limits).RespondBytes(Pdf).RespondBytes(Pdf, "application/zip");
            using var client = Client();

            await client.RequestGraphQLAsync("{ currentUser { eid } }");
            await client.FillPdfAsync("cast1", new { data = new { } }, AnvilClient.VERSION_LATEST);
            await client.DownloadDocumentsAsync("group1");

            Assert.Equal(new[] { "/graphql", "/api/v1/fill/cast1.pdf?versionNumber=-1", "/api/document-group/group1.zip" }, _server.Requests.Select(r => r.PathAndQuery));
            Assert.Equal(new[] { "POST", "POST", "GET" }, _server.Requests.Select(r => r.Method));
            Assert.StartsWith("application/json", _server.Requests[1].ContentType);
            Assert.Equal(@"{""data"":{}}", _server.Requests[1].BodyText);
        }

        [Fact]
        public async Task ALargePdf_ComesBackIntact_AsBytesAndAsAStream()
        {
            var big = new byte[3 * 1024 * 1024];
            new Random(1).NextBytes(big);
            Array.Copy(Encoding.ASCII.GetBytes("%PDF-"), big, 5);
            _server.RespondBytes(big, "application/pdf", Limits).RespondBytes(big);
            using var client = Client();

            Assert.Equal(big, await client.GeneratePdfAsync(new { data = new[] { new { content = "x" } } }));

            using var stream = await client.GeneratePdfStreamAsync(new { data = new[] { new { content = "x" } } });
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(big, buffer.ToArray());
        }

        // ------------------------------------------------------------------
        // Rate limiting

        [Fact]
        public async Task TheLimit_IsLearned_AndConcurrentRequestsArePaced()
        {
            _server.RespondJson("{}", ("X-RateLimit-Limit", "2"), ("X-RateLimit-Interval-Ms", "300"));
            using var client = Client();

            var clock = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x")));

            Assert.Equal(4, _server.Requests.Count);
            Assert.Equal(2, client.RateLimit.Limit);
            Assert.True(client.RateLimit.LearnedFromResponse);
            Assert.True(clock.ElapsedMilliseconds >= 300, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task A429_IsRetriedAfterRetryAfter()
        {
            _server.Respond(429, "Too many", "text/plain", ("Retry-After", "0.2")).RespondJson(@"{""data"":{""ok"":true}}", Limits);
            using var client = Client();
            var rateLimited = 0;
            client.RateLimited += (_, _) => rateLimited++;

            var clock = Stopwatch.StartNew();
            var data = await client.RequestGraphQLAsync("{ ok }");

            Assert.True((bool)data["ok"]!);
            Assert.Equal(2, _server.Requests.Count);
            Assert.Equal(1, rateLimited);
            Assert.True(clock.ElapsedMilliseconds >= 200, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ClientsWithTheSameKey_ShareTheLearnedLimit()
        {
            _server.RespondJson("{}", ("X-RateLimit-Limit", "7"), ("X-RateLimit-Interval-Ms", "700"));
            var key = "shared-" + Guid.NewGuid().ToString("N");
            using var a = Client(o => o.ApiKey = key);
            using var b = Client(o => o.ApiKey = key);

            await a.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(7, b.RateLimit.Limit);
        }

        // ------------------------------------------------------------------
        // Errors

        [Fact]
        public async Task GraphQLErrors_AreStructured()
        {
            _server.RespondJson(@"{""data"":{""etchPacket"":null},""errors"":[{""message"":""Not found"",""name"":""NotFoundError"",""path"":[""etchPacket""]}]}", Limits);
            using var client = Client();

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.GetEtchPacketAsync("nope"));

            Assert.Equal("200: NotFoundError: Not found", ex.Message);
            Assert.Equal("etchPacket", Assert.Single(ex.Errors).Raw!["path"]![0]!.ToString());
            Assert.Equal(JTokenType.Null, ex.ResponseData!["etchPacket"]!.Type);
        }

        [Fact]
        public async Task RestValidationErrors_AreStructured()
        {
            _server.Respond(400, @"{""name"":""ValidationError"",""message"":""Invalid"",""fields"":[{""property"":""data"",""message"":""Required""}]}", "application/json", Limits);
            using var client = Client();

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.FillPdfAsync("cast1", new { }));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("400: ValidationError: Invalid (data: Required)", ex.Message);
            Assert.Contains("Content-Type", ex.Headers.Keys);
        }

        [Fact]
        public async Task NonJsonAndEmptyErrors_AreStructured()
        {
            _server.Respond(502, "<html>Bad gateway</html>", "text/html", Limits).Respond(500, "", "text/plain");
            using var client = Client();

            var proxy = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));
            Assert.Equal("502: Bad Gateway", proxy.Message);
            Assert.Equal("<html>Bad gateway</html>", proxy.ResponseContent);

            var empty = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));
            Assert.Equal("500: Internal Server Error", empty.Message);
        }

        [Fact]
        public async Task AJsonBodyThatCannotBeParsed_IsAnError()
        {
            _server.RespondJson(@"{""data"": {""cut"": ", Limits);
            using var client = Client();

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));

            Assert.Equal("JsonReaderException", Assert.Single(ex.Errors).Name);
        }

        [Fact]
        public async Task A5xx_IsRetried()
        {
            _server.Respond(503, "down", "text/plain", Limits).RespondJson("{}");
            using var client = Client(o => o.MaxRetries = 2);

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(2, _server.Requests.Count);
        }

        [Fact]
        public async Task AConnectionFailure_IsRetriedWithBackoff_ThenThrown()
        {
            using var client = new AnvilClient(new AnvilClientOptions { ApiKey = "k", BaseUrl = LoopbackServer.ClosedUrl(), MaxRetries = 2 });

            var clock = Stopwatch.StartNew();
            await Assert.ThrowsAsync<HttpRequestException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            // 100ms then 200ms of backoff
            Assert.True(clock.ElapsedMilliseconds >= 300, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ARequestThatTimesOut_Throws_AndIsNotRetried()
        {
            _server.RespondAfter(1000);
            using var client = Client(o =>
            {
                o.Timeout = TimeSpan.FromMilliseconds(100);
                o.MaxRetries = 3;
            });

            await Assert.ThrowsAsync<TimeoutException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            await Task.Delay(50);
            Assert.Single(_server.Requests);
        }

        // ------------------------------------------------------------------
        // Uploads

        [Fact]
        public async Task AMultipartUpload_ArrivesAsOperationsMapAndFile()
        {
            _server.RespondJson(@"{""data"":{""createEtchPacket"":{""eid"":""p1""}}}", Limits);
            using var client = Client();
            var path = Path.Combine(Path.GetTempPath(), $"anvil-wire-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(path, Pdf);

            try
            {
                var packet = await client.CreateEtchPacketAsync(new CreateEtchPacketPayload
                {
                    Name = "Wire",
                    Files = new List<Anvil.Types.GraphQL.EtchFile> { new DocumentUpload { Id = "f", File = AnvilClient.PrepareGraphQLFile(path) } },
                });

                Assert.Equal("p1", packet.Eid);
                var request = _server.LastRequest;
                Assert.StartsWith("multipart/form-data", request.ContentType);
                var parts = Multipart.Parse(request.ContentType!, request.Body);
                Assert.Equal(new[] { "operations", "map", "1" }, parts.Select(p => p.Name));
                var operations = JObject.Parse(parts[0].Text);
                Assert.Equal(JTokenType.Null, operations["variables"]!["files"]![0]!["file"]!.Type);
                Assert.Equal("Wire", operations["variables"]!["name"]!.ToString());
                Assert.Equal(@"{""1"":[""variables.files.0.file""]}", parts[1].Text);
                Assert.Equal(Path.GetFileName(path), parts[2].Filename);
                Assert.Equal("application/pdf", parts[2].Headers["Content-Type"]);
                Assert.Equal(Pdf, parts[2].Body);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task AnUpload_IsSentInFullAgainAfterA429()
        {
            _server.Respond(429, "", "text/plain", ("Retry-After", "0")).RespondJson(@"{""data"":{""createCast"":{""eid"":""c""}}}", Limits);
            using var client = Client();

            await client.RequestGraphQLAsync("mutation ($file: Upload!) { createCast(file: $file) { eid } }", new { file = AnvilClient.PrepareGraphQLFile(Pdf, "doc.pdf") });

            Assert.Equal(2, _server.Requests.Count);
            foreach (var request in _server.Requests)
            {
                Assert.Equal(Pdf, Multipart.Parse(request.ContentType!, request.Body)[2].Body);
            }
        }
    }
}
