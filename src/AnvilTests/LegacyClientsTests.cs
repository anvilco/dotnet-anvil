#pragma warning disable CS0618 // The pre-1.0 clients and payloads are obsolete, and still have to work
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Anvil;
using Anvil.Client;
using Anvil.GraphQL;
using Anvil.Payloads.Request.Types;

using AnvilTests.Support;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    /// <summary>The pre-1.0 RestClient and GraphQLClient keep working through AnvilClient.</summary>
    public class LegacyClientsTests
    {
        private static readonly (string, string)[] Limits = { ("X-RateLimit-Limit", "100"), ("X-RateLimit-Interval-Ms", "1000") };

        private static async Task<byte[]> ReadAll(Stream stream)
        {
            using (stream)
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                return buffer.ToArray();
            }
        }

        [Fact]
        public async Task RestClient_FillPdf_Variants()
        {
            var handler = new FakeHandler().RespondPdf(null, Limits).RespondPdf().RespondPdf().RespondPdf();
            using var client = new RestClient(Clients.Create(handler));
            var payload = new Anvil.Payloads.Request.FillPdf
            {
                Title = "T",
                Data = new Dictionary<string, dynamic> { ["name"] = "Sally" },
            };

            Assert.Equal(FakeHandler.Pdf, await ReadAll(await client.FillPdf("cast1", payload)));
            Assert.Equal("/api/v1/fill/cast1.pdf", handler.LastRequest.PathAndQuery);
            Assert.Equal("Sally", JObject.Parse(handler.LastRequest.Body!)["data"]!["name"]!.ToString());

            Assert.Equal(FakeHandler.Pdf, await ReadAll(await client.FillPdf("cast1", payload, ClientConstants.VERSION_LATEST)));
            Assert.Equal("/api/v1/fill/cast1.pdf?versionNumber=-1", handler.LastRequest.PathAndQuery);

            var path = Path.Combine(Path.GetTempPath(), $"anvil-legacy-{Guid.NewGuid():N}.pdf");
            try
            {
                Assert.True(await client.FillPdf("cast1", payload, path));
                Assert.Equal(FakeHandler.Pdf, File.ReadAllBytes(path));
                Assert.True(await client.FillPdf("cast1", payload, path, 3));
                Assert.Equal("/api/v1/fill/cast1.pdf?versionNumber=3", handler.LastRequest.PathAndQuery);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task RestClient_GeneratePdf_AndDownloadDocuments()
        {
            var handler = new FakeHandler().RespondPdf(null, Limits).RespondPdf().RespondPdf();
            using var client = new RestClient(Clients.Create(handler));
            var payload = new Anvil.Payloads.Request.GeneratePdf
            {
                Title = "T",
                Type = "markdown",
                Data = new List<IGeneratePdfListable>
                {
                    new GeneratePdfItem { Label = "L", Content = "C" },
                    new GeneratePdfTable { Table = new GeneratePdfTableContent { Rows = new List<List<string>> { new List<string> { "a" } } } },
                },
            };

            var content = await client.GeneratePdf(payload);
            Assert.Equal(FakeHandler.Pdf, await content.ReadAsByteArrayAsync());
            Assert.Equal("/api/v1/generate-pdf", handler.LastRequest.PathAndQuery);
            Assert.Equal("C", JObject.Parse(handler.LastRequest.Body!)["data"]![0]!["content"]!.ToString());

            var path = Path.Combine(Path.GetTempPath(), $"anvil-legacy-{Guid.NewGuid():N}.pdf");
            try
            {
                Assert.True(await client.GeneratePdf(payload, path));
                Assert.Equal(FakeHandler.Pdf, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }

            Assert.Equal(FakeHandler.Pdf, await ReadAll(await client.DownloadDocuments("group1")));
            Assert.Equal("/api/document-group/group1.zip", handler.LastRequest.PathAndQuery);
        }

        [Fact]
        public async Task RestClient_SendRequests_AreRelativeToApiV1()
        {
            var handler = new FakeHandler().RespondJson("{}", Limits).RespondJson("{}").Respond(404, "Not Found", "text/plain");
            using var client = new RestClient(Clients.Create(handler));

            await client.SendGetRequest("something");
            Assert.Equal("/api/v1/something", handler.LastRequest.PathAndQuery);

            await client.SendPostRequest("generate-pdf", new StringContent(@"{""a"":1}", Encoding.UTF8, "application/json"));
            Assert.Equal("/api/v1/generate-pdf", handler.LastRequest.PathAndQuery);
            Assert.Equal(@"{""a"":1}", handler.LastRequest.Body);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.SendGetRequest("/api/document-group/nope.zip"));
            Assert.Equal(404, ex.StatusCode);
            Assert.Equal("/api/document-group/nope.zip", handler.LastRequest.PathAndQuery);
        }

        [Fact]
        public void TheOldPayloads_ValidateTheirInputs()
        {
            var generate = new Anvil.Payloads.Request.GeneratePdf();
            Assert.Throws<ArgumentException>(() => generate.Type = "docx");
            Assert.Throws<ArgumentException>(() => generate.Data = "a string");
            generate.Type = "html";
            generate.Data = new GeneratePdfHtml { Html = "<p>x</p>", Css = "p {}" };
            Assert.Equal("html", generate.Type);

            var signer = new EtchSigner { SignerType = "in-person", RedirectUrl = "https://example.com" };
            Assert.Throws<ArgumentNullException>(() => signer.SignerType = null!);
            var json = JObject.Parse(JsonConvert.SerializeObject(signer, AnvilClient.SerializerSettings));
            Assert.Equal("https://example.com", json["redirectURL"]!.ToString());

            var files = new IEtchPacketAttachable[]
            {
                new EtchCastRef { Id = "a", CastEid = "cast1" },
                new DocumentUpload { Id = "b", File = new Base64FileUpload { Data = "x", Filename = "x.pdf" }, Fields = new List<SignatureField> { new SignatureField { Id = "s", Type = "signature", PageNum = 0, Rect = new Rect { X = 1, Y = 2, Width = 3, Height = 4 } } } },
                new DocumentMarkup { Id = "c", Markup = new HtmlCssMarkup { Html = "<p/>" } },
                new DocumentMarkdown { Id = "d", Fields = new List<IGeneratePdfListable> { new GeneratePdfItem { Content = "md" } } },
                new EtchFile { Name = "e", Type = "pdf", Filename = "e.pdf" },
            };
            var packet = new Anvil.Payloads.Request.CreateEtchPacket
            {
                Name = "P",
                Files = files,
                Signers = new[] { signer },
                MergePdfs = true,
                WebhookUrl = "https://example.com/hook",
                SignaturePageOptions = new { },
                ReplyToEmail = "ops@example.com",
                ReplyToName = "Ops",
                SignatureEmailBody = "b",
                SignatureEmailSubject = "s",
                Data = new { },
            };
            json = JObject.Parse(JsonConvert.SerializeObject(packet, AnvilClient.SerializerSettings));
            Assert.Equal("cast1", json["files"]![0]!["castEid"]!.ToString());
            Assert.Equal("x.pdf", json["files"]![1]!["file"]!["filename"]!.ToString());
            Assert.Equal(4, (int)json["files"]![1]!["fields"]![0]!["rect"]!["height"]!);
            Assert.Equal("<p/>", json["files"]![2]!["markup"]!["html"]!.ToString());
            Assert.Equal("md", json["files"]![3]!["fields"]![0]!["content"]!.ToString());
            Assert.True((bool)json["mergePDFs"]!);
            Assert.Equal("https://example.com/hook", json["webhookURL"]!.ToString());
            Assert.False((bool)json["isDraft"]!);
            Assert.True((bool)json["isTest"]!);
        }

        [Fact]
        public async Task GraphQLClient_Methods_UseTheNewOperations_AndOldPayloadTypes()
        {
            var handler = new FakeHandler()
                .RespondJson(@"{""data"":{""currentUser"":{""eid"":""u1""}}}", Limits)
                .RespondJson(@"{""data"":{""etchPacket"":{""eid"":""p1"",""name"":""P"",""detailsURL"":""https://x"",""documentGroup"":{""eid"":""g1"",""status"":""sent"",""files"":[{""name"":""f"",""type"":""pdf"",""filename"":""f.pdf""}],""signers"":[{""eid"":""s1"",""routingOrder"":1,""signActionType"":""sign""}]}}}}")
                .RespondJson(@"{""data"":{""generateEtchSignURL"":""https://sign""}}")
                .RespondJson(@"{""data"":{""forgeSubmit"":{""eid"":""sub1"",""currentStep"":1,""weldData"":{""eid"":""wd1""}}}}")
                .RespondJson(@"{""data"":{""removeWeldData"":true}}")
                .RespondJson(@"{""data"":{""currentUser"":{""eid"":""u1"",""name"":""Me""}}}")
                .RespondJson(@"{""data"":{""createEtchPacket"":{""eid"":""p2"",""documentGroup"":{""eid"":""g2""}}}}");
            using var client = new GraphQLClient(Clients.Create(handler));

            var raw = await client.SendQuery("{ currentUser { eid } }", null);
            Assert.Equal("u1", raw["currentUser"]!["eid"]!.ToString());

            var packet = await client.GetEtchPacket("p1");
            Assert.Equal("p1", packet.EtchPacket!.Eid);
            Assert.Equal("https://x", packet.EtchPacket.DetailsURL);
            Assert.Equal("g1", packet.EtchPacket.DocumentGroup!.Eid);
            Assert.Equal("f.pdf", packet.EtchPacket.DocumentGroup.Files![0].Filename);
            Assert.Equal("sign", packet.EtchPacket.DocumentGroup.Signers![0].SignActionType);
            Assert.Equal(Operations.EtchPacketQuery(), JObject.Parse(handler.LastRequest.Body!)["query"]!.ToString());

            var url = await client.GenerateEtchSignUrl("s1", "user-1");
            Assert.Equal("https://sign", url.GenerateEtchSignUrl);

            var submission = await client.ForgeSubmit(new Anvil.Payloads.Request.ForgeSubmit { ForgeEid = "f1", Payload = new { }, IsTest = true, CurrentStep = 1, Complete = false, Timezone = "UTC", GroupArrayId = "g", GroupArrayIndex = 0, WeldDataEid = "w", SubmissionEid = "s" });
            Assert.Equal("sub1", submission.ForgeSubmit!.Eid);
            Assert.Equal("wd1", ((JObject)submission.ForgeSubmit.WeldData!)["eid"]!.ToString());
            Assert.Equal(Operations.ForgeSubmitMutation(), JObject.Parse(handler.LastRequest.Body!)["query"]!.ToString());

            var removed = await client.RemoveWeldData("wd1");
            Assert.True(removed.RemoveWeldData);

            var user = await client.GetCurrentUser();
            Assert.Equal("Me", user.CurrentUser!["name"]!.ToString());

            var created = await client.CreateEtchPacket(new Anvil.Payloads.Request.CreateEtchPacket { Name = "P" });
            Assert.Equal("p2", ((JObject)created.CreateEtchPacket!)["eid"]!.ToString());
            Assert.Equal(Operations.CreateEtchPacketMutation(), JObject.Parse(handler.LastRequest.Body!)["query"]!.ToString());
        }

        [Fact]
        public void TheOldResponseTypes_StillDeserialize()
        {
            var created = JsonConvert.DeserializeObject<Anvil.Payloads.Response.CreateEtchPacketData>(@"{""id"":""1"",""eid"":""p"",""name"":""n"",""detailsURL"":""https://x"",""documentGroup"":{""eid"":""g""}}", AnvilClient.SerializerSettings)!;
            Assert.Equal("https://x", created.DetailsUrl);
            Assert.Equal("g", created.DocumentGroup!["eid"]!.ToString());

            var forge = JsonConvert.DeserializeObject<Anvil.Payloads.Response.ForgeSubmitData>(@"{""id"":1,""eid"":""s"",""payloadValue"":{},""isComplete"":true,""currentStep"":2,""createdAt"":""2026-01-01T00:00:00Z"",""updatedAt"":""2026-01-01T00:00:00Z"",""signer"":{},""weldData"":{}}", AnvilClient.SerializerSettings)!;
            Assert.True(forge.IsComplete);
            Assert.Equal(2026, forge.CreatedAt!.Value.Year);

            var error = JsonConvert.DeserializeObject<Anvil.Payloads.Response.Error>(@"{""name"":""ValidationError"",""fields"":[{""message"":""m"",""property"":""p""}]}", AnvilClient.SerializerSettings)!;
            Assert.Equal("p", error.Fields![0].Property);

            Assert.Null(new Anvil.Payloads.Request.GenerateEtchSignUrlPayload().GenerateEtchSignUrl);
        }

        private class FakeBaseClient : BaseClient
        {
            public FakeBaseClient(string? apiKey) => _apiKey = apiKey;
            public string Encode() => EncodeApiKey();
        }

        [Fact]
        public void BaseClient_EncodesTheKeyWithItsColon()
        {
            Assert.Equal(Convert.ToBase64String(Encoding.ASCII.GetBytes("my-api-key:")), new FakeBaseClient("my-api-key").Encode());
            Assert.Throws<AnvilClientException>(() => new FakeBaseClient(" ").Encode());
        }

        [Fact]
        public void TheOldQueryClasses_StillFormat()
        {
            Assert.Equal(Operations.GenerateEtchSignUrlMutation(), new Anvil.Queries.GenerateEtchSignUrl().GetFullDefaultQuery());
            Assert.Equal(Operations.CurrentUserQuery(), new Anvil.Queries.CurrentUser().GetFullDefaultQuery());
            Assert.Null(new Anvil.Queries.GenerateEtchSignUrl().DefaultResponseQuery);
            Assert.NotNull(new Anvil.Queries.CurrentUser().DefaultResponseQuery);

            var custom = new CustomQuery();
            Assert.Equal("query { x { eid } }", custom.GetFullDefaultQuery());
        }

        private class CustomQuery : Anvil.Queries.BaseQuery
        {
            public override string DefaultQuery => "query {{ x {0} }}";
            public override string? DefaultResponseQuery => "{ eid }";
        }
    }
}
