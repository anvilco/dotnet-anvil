using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Anvil;
using Anvil.GraphQL;
using Anvil.Payloads;

using AnvilTests.Support;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    public class AnvilClientMethodsTests
    {
        private static readonly (string, string)[] Limits = { ("X-RateLimit-Limit", "100"), ("X-RateLimit-Interval-Ms", "1000") };

        private static FakeHandler Pdf() => new FakeHandler().RespondPdf(null, Limits);

        private static FakeHandler Json(string json) => new FakeHandler().RespondJson(json, Limits);

        private static JObject Body(FakeHandler handler) => JObject.Parse(handler.LastRequest.Body!);

        // ------------------------------------------------------------------
        // FillPdf

        [Fact]
        public async Task FillPdf_PostsThePayload_AndReturnsThePdf()
        {
            var handler = Pdf();
            using var client = Clients.Create(handler);

            var pdf = await client.FillPdfAsync("cast1", new FillPdfPayload
            {
                Title = "Filled",
                Data = new Dictionary<string, object> { ["name"] = "Sally", ["phone"] = new { num = "5551231234", region = "US" } },
            });

            Assert.Equal(FakeHandler.Pdf, pdf);
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            Assert.Equal("/api/v1/fill/cast1.pdf", handler.LastRequest.PathAndQuery);
            var body = Body(handler);
            Assert.Equal("Filled", body["title"]!.ToString());
            Assert.Equal("Sally", body["data"]!["name"]!.ToString());
            Assert.Equal("US", body["data"]!["phone"]!["region"]!.ToString());
        }

        [Theory]
        [InlineData(0, "?versionNumber=0")]
        [InlineData(5, "?versionNumber=5")]
        [InlineData(AnvilClient.VERSION_LATEST, "?versionNumber=-1")]
        [InlineData(AnvilClient.VERSION_LATEST_PUBLISHED, "?versionNumber=-2")]
        public async Task FillPdf_SendsTheVersionNumber_WhenGiven(int versionNumber, string query)
        {
            var handler = Pdf();
            using var client = Clients.Create(handler);

            await client.FillPdfAsync("cast1", new { data = new { } }, versionNumber);

            Assert.Equal("/api/v1/fill/cast1.pdf" + query, handler.LastRequest.PathAndQuery);
        }

        [Fact]
        public async Task FillPdf_AcceptsAnyPayloadShape()
        {
            var handler = Pdf().RespondPdf(null, Limits).RespondPdf(null, Limits);
            using var client = Clients.Create(handler);

            await client.FillPdfAsync("cast1", new { Data = new { Name = "Sally" } });
            Assert.Equal("Sally", Body(handler)["data"]!["name"]!.ToString());

            await client.FillPdfAsync("cast1", new Dictionary<string, object> { ["data"] = new Dictionary<string, object> { ["name"] = "Sally" } });
            Assert.Equal("Sally", Body(handler)["data"]!["name"]!.ToString());

            await client.FillPdfAsync("cast1", @"{ ""data"": { ""name"": ""Sally"" } }");
            Assert.Equal("Sally", Body(handler)["data"]!["name"]!.ToString());
        }

        [Fact]
        public async Task FillPdf_StreamAndFileVariants()
        {
            var handler = Pdf().RespondPdf(null, Limits);
            using var client = Clients.Create(handler);

            using (var stream = await client.FillPdfStreamAsync("cast1", new { data = new { } }))
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                Assert.Equal(FakeHandler.Pdf, buffer.ToArray());
            }

            var path = Path.Combine(Path.GetTempPath(), $"anvil-test-{Guid.NewGuid():N}.pdf");
            try
            {
                await client.FillPdfToFileAsync("cast1", new { data = new { } }, path, AnvilClient.VERSION_LATEST);
                Assert.Equal(FakeHandler.Pdf, File.ReadAllBytes(path));
                Assert.EndsWith("?versionNumber=-1", handler.LastRequest.PathAndQuery);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task FillPdf_EscapesTheTemplateId_AndRequiresIt()
        {
            var handler = Pdf();
            using var client = Clients.Create(handler);

            await client.FillPdfAsync("a b/c", new { data = new { } });
            Assert.Equal("/api/v1/fill/a%20b%2Fc.pdf", handler.LastRequest.PathAndQuery);

            await Assert.ThrowsAsync<ArgumentException>(() => client.FillPdfAsync("", new { data = new { } }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.FillPdfAsync("cast1", null!));
        }

        [Fact]
        public async Task FillPdf_WithOutputFormatUrl_ReturnsTheJsonBytes()
        {
            var handler = Json(@"{""url"":""https://example.com/filled.pdf""}");
            using var client = Clients.Create(handler);

            var bytes = await client.FillPdfAsync("cast1", new FillPdfPayload { Data = new { }, OutputFormat = "url" });

            Assert.Equal("https://example.com/filled.pdf", JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes))["url"]!.ToString());
            Assert.Equal("url", Body(handler)["outputFormat"]!.ToString());
        }

        // ------------------------------------------------------------------
        // GeneratePdf

        [Fact]
        public async Task GeneratePdf_Markdown()
        {
            var handler = Pdf();
            using var client = Clients.Create(handler);

            var pdf = await client.GeneratePdfAsync(new GeneratePdfPayload
            {
                Title = "Markdown",
                Data = new List<MarkdownContent>
                {
                    new MarkdownContent { Label = "Hello", Content = "World" },
                    new MarkdownContent { Table = new MarkdownTable { FirstRowHeaders = true, Rows = new List<List<string?>?> { new List<string?> { "a", "b" } } } },
                },
                Page = new Anvil.Types.Rest.PageOptions { Width = "8.5in" },
            });

            Assert.Equal(FakeHandler.Pdf, pdf);
            Assert.Equal("/api/v1/generate-pdf", handler.LastRequest.PathAndQuery);
            var body = Body(handler);
            Assert.Equal("Markdown", body["title"]!.ToString());
            Assert.Null(body["type"]);
            Assert.Equal("World", body["data"]![0]!["content"]!.ToString());
            Assert.True((bool)body["data"]![1]!["table"]!["firstRowHeaders"]!);
            Assert.Equal("8.5in", body["page"]!["width"]!.ToString());
        }

        [Fact]
        public async Task GeneratePdf_Html()
        {
            var handler = Pdf();
            using var client = Clients.Create(handler);

            await client.GeneratePdfAsync(new GeneratePdfPayload
            {
                Type = GeneratePdfPayload.TypeValues.Html,
                Data = new HtmlContent { Html = "<h1>Hi</h1>", Css = "h1 { color: red }" },
            });

            var body = Body(handler);
            Assert.Equal("html", body["type"]!.ToString());
            Assert.Equal("<h1>Hi</h1>", body["data"]!["html"]!.ToString());
        }

        [Fact]
        public async Task GeneratePdf_StreamAndFileVariants()
        {
            var handler = Pdf().RespondPdf(null, Limits);
            using var client = Clients.Create(handler);

            using (var stream = await client.GeneratePdfStreamAsync(new { data = new[] { new { content = "x" } } }))
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                Assert.Equal(FakeHandler.Pdf, buffer.ToArray());
            }

            var path = Path.Combine(Path.GetTempPath(), $"anvil-test-{Guid.NewGuid():N}.pdf");
            try
            {
                await client.GeneratePdfToFileAsync(new { data = new[] { new { content = "x" } } }, path);
                Assert.Equal(FakeHandler.Pdf, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ------------------------------------------------------------------
        // DownloadDocuments

        [Fact]
        public async Task DownloadDocuments_GetsTheZip()
        {
            var zip = new byte[] { (byte)'P', (byte)'K', 3, 4 };
            var handler = new FakeHandler().RespondPdf(zip, Limits).RespondPdf(zip, Limits);
            using var client = Clients.Create(handler);

            Assert.Equal(zip, await client.DownloadDocumentsAsync("group1"));
            Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
            Assert.Equal("/api/document-group/group1.zip", handler.LastRequest.PathAndQuery);
            Assert.Null(handler.LastRequest.Body);

            using var stream = await client.DownloadDocumentsStreamAsync("group1");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(zip, buffer.ToArray());
        }

        // ------------------------------------------------------------------
        // CreateEtchPacket

        private const string PacketResponse = @"{""data"":{""createEtchPacket"":{
            ""eid"":""packet1"",""name"":""Packet"",""status"":""sent"",""isTest"":true,""detailsURL"":""https://app.useanvil.com/packet1"",
            ""documentGroup"":{""eid"":""group1"",""status"":""sent"",""files"":[{""name"":""file1""}],""signers"":[{""eid"":""signer1"",""name"":""Sally"",""routingOrder"":1}]}
        }}}";

        [Fact]
        public async Task CreateEtchPacket_SendsTheMutation_AndReturnsThePacket()
        {
            var handler = Json(PacketResponse);
            using var client = Clients.Create(handler);

            var packet = await client.CreateEtchPacketAsync(new CreateEtchPacketPayload
            {
                Name = "Packet",
                Files = new List<Anvil.Types.GraphQL.EtchFile>
                {
                    new EtchCastRef { Id = "template", CastEid = "cast1" },
                    new DocumentUpload
                    {
                        Id = "upload",
                        Title = "Upload",
                        File = new Base64Upload { Data = "JVBERi0=", Filename = "doc.pdf" },
                        Fields = new List<object> { new SignatureField { Id = "sig", Type = SignatureField.TypeValues.Signature, PageNum = 0, Rect = new Rect { X = 1, Y = 2, Width = 3, Height = 4 } } },
                    },
                },
                Signers = new List<object>
                {
                    new EtchSigner
                    {
                        Id = "signer",
                        Name = "Sally",
                        Email = "sally@example.com",
                        SignerType = EtchSigner.SignerTypeValues.Embedded,
                        Fields = new List<object> { new SignerField { FileId = "template", FieldId = "sig1" } },
                    },
                },
                WebhookUrl = "https://example.com/hook",
            });

            Assert.Equal("packet1", packet.Eid);
            Assert.Equal("https://app.useanvil.com/packet1", packet.DetailsUrl);
            Assert.Equal("signer1", Assert.Single(packet.DocumentGroup!.Signers!)!.Eid);

            var body = Body(handler);
            Assert.Equal(Operations.CreateEtchPacketMutation(), body["query"]!.ToString());
            Assert.Equal("CreateEtchPacket", body["operationName"]!.ToString());
            var variables = (JObject)body["variables"]!;
            Assert.Equal("Packet", variables["name"]!.ToString());
            Assert.False((bool)variables["isDraft"]!);
            Assert.True((bool)variables["isTest"]!);
            Assert.Equal("cast1", variables["files"]![0]!["castEid"]!.ToString());
            Assert.Equal("doc.pdf", variables["files"]![1]!["file"]!["filename"]!.ToString());
            Assert.Equal("application/pdf", variables["files"]![1]!["file"]!["mimetype"]!.ToString());
            Assert.Equal(0, (int)variables["files"]![1]!["fields"]![0]!["pageNum"]!);
            Assert.Equal("embedded", variables["signers"]![0]!["signerType"]!.ToString());
            Assert.Equal("sig1", variables["signers"]![0]!["fields"]![0]!["fieldId"]!.ToString());
            Assert.Equal("https://example.com/hook", variables["webhookURL"]!.ToString());
        }

        [Theory]
        [InlineData("{ eid status }")]
        [InlineData("eid status")]
        public async Task CreateEtchPacket_WithAResponseQuery_SelectsIt(string responseQuery)
        {
            var handler = Json(@"{""data"":{""createEtchPacket"":{""eid"":""packet1"",""status"":""draft"",""customField"":42}}}");
            using var client = Clients.Create(handler);

            var packet = await client.CreateEtchPacketAsync(new { name = "x" }, responseQuery);

            Assert.Equal("draft", packet.Status);
            Assert.Equal(42, (int)packet.AdditionalData!["customField"]!);
            var query = Body(handler)["query"]!.ToString();
            Assert.Contains(") { eid status }", query);
            Assert.DoesNotContain("documentGroup", query);
        }

        [Fact]
        public async Task CreateEtchPacket_WithAMutation_SendsItVerbatim()
        {
            var handler = Json(@"{""data"":{""createEtchPacket"":{""eid"":""packet1""}}}");
            using var client = Clients.Create(handler);
            const string mutation = "mutation Mine($name: String) { createEtchPacket(name: $name) { eid } }";

            await client.CreateEtchPacketAsync(new { name = "x" }, mutation: mutation);

            var body = Body(handler);
            Assert.Equal(mutation, body["query"]!.ToString());
            Assert.Null(body["operationName"]);
        }

        [Fact]
        public async Task CreateEtchPacket_WithAnInPersonSigner()
        {
            var handler = Json(PacketResponse);
            using var client = Clients.Create(handler);

            await client.CreateEtchPacketAsync(new CreateEtchPacketPayload
            {
                Name = "In person",
                Signers = new List<object> { new EtchSigner { Id = "s", Name = "Sally", Email = "sally@example.com", SignerType = EtchSigner.SignerTypeValues.InPerson } },
            });

            Assert.Equal("in-person", Body(handler)["variables"]!["signers"]![0]!["signerType"]!.ToString());
        }

        [Fact]
        public async Task GraphQLErrors_Throw()
        {
            var handler = Json(@"{""data"":{""createEtchPacket"":null},""errors"":[{""message"":""Files are required"",""name"":""ValidationError""}]}");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.CreateEtchPacketAsync(new { name = "x" }));

            Assert.Equal("200: ValidationError: Files are required", ex.Message);
        }

        [Fact]
        public async Task AMissingField_Throws()
        {
            var handler = Json(@"{""data"":{""somethingElse"":{}}}");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.CreateEtchPacketAsync(new { name = "x" }));

            Assert.Contains("no `createEtchPacket`", ex.Message);
        }

        // ------------------------------------------------------------------
        // GetEtchPacket, GenerateEtchSignUrl

        [Fact]
        public async Task GetEtchPacket_QueriesByEid()
        {
            var handler = Json(@"{""data"":{""etchPacket"":{""eid"":""packet1"",""status"":""completed"",""numberRemainingSigners"":0}}}");
            using var client = Clients.Create(handler);

            var packet = await client.GetEtchPacketAsync("packet1");

            Assert.Equal("completed", packet.Status);
            Assert.Equal(0, packet.NumberRemainingSigners);
            var body = Body(handler);
            Assert.Equal(Operations.EtchPacketQuery(), body["query"]!.ToString());
            Assert.Equal("packet1", body["variables"]!["eid"]!.ToString());
            Assert.Equal("EtchPacket", body["operationName"]!.ToString());
        }

        [Fact]
        public async Task GetEtchPacket_WithAResponseQuery()
        {
            var handler = Json(@"{""data"":{""etchPacket"":{""eid"":""packet1""}}}");
            using var client = Clients.Create(handler);

            await client.GetEtchPacketAsync("packet1", "eid");

            Assert.Equal(Operations.EtchPacketQuery("{ eid }"), Body(handler)["query"]!.ToString());
        }

        [Fact]
        public async Task GenerateEtchSignUrl_ReturnsTheUrl()
        {
            var handler = Json(@"{""data"":{""generateEtchSignURL"":""https://app.useanvil.com/sign/abc""}}");
            using var client = Clients.Create(handler);

            var url = await client.GenerateEtchSignUrlAsync("signer1", "user-42");

            Assert.Equal("https://app.useanvil.com/sign/abc", url);
            var body = Body(handler);
            Assert.Equal(Operations.GenerateEtchSignUrlMutation(), body["query"]!.ToString());
            Assert.Equal("signer1", body["variables"]!["signerEid"]!.ToString());
            Assert.Equal("user-42", body["variables"]!["clientUserId"]!.ToString());
        }

        // ------------------------------------------------------------------
        // ForgeSubmit, RemoveWeldData

        [Fact]
        public async Task ForgeSubmit_ReturnsTheSubmission()
        {
            var handler = Json(@"{""data"":{""forgeSubmit"":{""eid"":""sub1"",""status"":""completed"",""continueURL"":""https://app.useanvil.com/continue"",""weldData"":{""eid"":""wd1"",""isTest"":true}}}}");
            using var client = Clients.Create(handler);

            var submission = await client.ForgeSubmitAsync(new ForgeSubmitPayload { ForgeEid = "forge1", Payload = new { name = "Sally" }, IsTest = true });

            Assert.Equal("sub1", submission.Eid);
            Assert.Equal("https://app.useanvil.com/continue", submission.ContinueUrl);
            Assert.Equal("wd1", submission.WeldData!.Eid);
            var body = Body(handler);
            Assert.Equal(Operations.ForgeSubmitMutation(), body["query"]!.ToString());
            Assert.Equal("forge1", body["variables"]!["forgeEid"]!.ToString());
            Assert.Equal("Sally", body["variables"]!["payload"]!["name"]!.ToString());
            Assert.True((bool)body["variables"]!["isTest"]!);
        }

        [Fact]
        public async Task ForgeSubmit_WithAResponseQueryAndAMutation()
        {
            var handler = Json(@"{""data"":{""forgeSubmit"":{""eid"":""sub1""}}}").RespondJson(@"{""data"":{""forgeSubmit"":{""eid"":""sub1""}}}", Limits);
            using var client = Clients.Create(handler);

            await client.ForgeSubmitAsync(new { forgeEid = "f", payload = new { } }, "eid");
            Assert.Equal(Operations.ForgeSubmitMutation("{ eid }"), Body(handler)["query"]!.ToString());

            await client.ForgeSubmitAsync(new { forgeEid = "f", payload = new { } }, mutation: "mutation M { forgeSubmit(forgeEid: \"f\", payload: {}) { eid } }");
            Assert.StartsWith("mutation M", Body(handler)["query"]!.ToString());
        }

        [Fact]
        public async Task RemoveWeldData_ReturnsWhetherItWasRemoved()
        {
            var handler = Json(@"{""data"":{""removeWeldData"":true}}");
            using var client = Clients.Create(handler);

            Assert.True(await client.RemoveWeldDataAsync("wd1"));
            var body = Body(handler);
            Assert.Equal(Operations.RemoveWeldDataMutation(), body["query"]!.ToString());
            Assert.Equal("wd1", body["variables"]!["eid"]!.ToString());
        }

        [Fact]
        public async Task GetCurrentUser_ReturnsTheUser()
        {
            var handler = Json(@"{""data"":{""currentUser"":{""eid"":""user1"",""email"":""me@example.com"",""organizations"":[{""eid"":""org1"",""slug"":""acme""}]}}}");
            using var client = Clients.Create(handler);

            var user = await client.GetCurrentUserAsync();

            Assert.Equal("user1", user.Eid);
            Assert.Equal("acme", Assert.Single(user.Organizations!)!.Slug);
            Assert.Equal(Operations.CurrentUserQuery(), Body(handler)["query"]!.ToString());
        }
    }
}
