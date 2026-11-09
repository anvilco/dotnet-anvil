using System.Collections.Generic;

using Anvil;
using Anvil.GraphQL;
using Anvil.Payloads;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    public class PayloadsTests
    {
        private static JObject Serialize(object value) => JObject.Parse(JsonConvert.SerializeObject(value, AnvilClient.SerializerSettings));

        [Fact]
        public void EtchSigner_DefaultsToEmail_AndAcceptsEveryType()
        {
            Assert.Equal("email", Serialize(new EtchSigner { Id = "s" })["signerType"]!.ToString());
            Assert.Equal("embedded", Serialize(new EtchSigner { Id = "s", SignerType = "embedded" })["signerType"]!.ToString());
            Assert.Equal("in-person", Serialize(new EtchSigner { Id = "s", SignerType = EtchSigner.SignerTypeValues.InPerson })["signerType"]!.ToString());
            Assert.Equal("someFutureType", Serialize(new EtchSigner { Id = "s", SignerType = "someFutureType" })["signerType"]!.ToString());
        }

        [Fact]
        public void EtchSigner_HasEveryDocumentedOption()
        {
            var json = Serialize(new EtchSigner
            {
                Id = "s",
                Name = "Sally",
                Email = "sally@example.com",
                RoutingOrder = 2,
                RedirectUrl = "https://example.com/done",
                EnableEmails = new[] { "etchComplete" },
                RequireIdCheck = "sms",
                IdCheckPhone = "+15551231234",
                SignatureExperience = EtchSigner.SignatureExperienceValues.SignWhileReviewing,
                SignatureMode = EtchSigner.SignatureModeValues.Draw,
                TokenValidForMinutes = 60,
                AccessCode = "1234",
                Fields = new List<object> { new SignerField { FileId = "f", FieldId = "sig", SignatureImageVerticalJustification = "bottom" } },
            });

            Assert.Equal(2, (int)json["routingOrder"]!);
            Assert.Equal("https://example.com/done", json["redirectURL"]!.ToString());
            Assert.Equal("etchComplete", json["enableEmails"]![0]!.ToString());
            Assert.Equal("sms", json["requireIdCheck"]!.ToString());
            Assert.Equal("signWhileReviewing", json["signatureExperience"]!.ToString());
            Assert.Equal(60, (int)json["tokenValidForMinutes"]!);
            Assert.Equal("bottom", json["fields"]![0]!["signatureImageVerticalJustification"]!.ToString());
        }

        [Fact]
        public void CreateEtchPacket_DefaultsToASentTestPacket()
        {
            var json = Serialize(new CreateEtchPacketPayload { Name = "Packet" });

            Assert.False((bool)json["isDraft"]!);
            Assert.True((bool)json["isTest"]!);
            Assert.Null(json["signers"]);
            Assert.Null(json["files"]);
        }

        [Fact]
        public void CreateEtchPacket_HasEveryMutationVariable()
        {
            var json = Serialize(new CreateEtchPacketPayload
            {
                Name = "Packet",
                OrganizationEid = "org1",
                SignatureEmailSubject = "Sign",
                SignatureEmailBody = "Please",
                SignatureProvider = "anvil",
                SignaturePageOptions = new { title = "Sign here" },
                Data = new { payloads = new { } },
                WebhookUrl = "https://example.com/hook",
                ReplyToName = "Ops",
                ReplyToEmail = "ops@example.com",
                EnableEmails = true,
                CreateCastTemplatesFromUploads = true,
                DuplicateCasts = true,
                MergePdfs = true,
                AllowUpdates = true,
            });

            foreach (var key in new[] { "organizationEid", "signatureEmailSubject", "signatureEmailBody", "signatureProvider", "signaturePageOptions", "data", "webhookURL", "replyToName", "replyToEmail", "enableEmails", "createCastTemplatesFromUploads", "duplicateCasts", "mergePDFs", "allowUpdates" })
            {
                Assert.True(json.ContainsKey(key), $"missing {key}");
            }
        }

        [Fact]
        public void Files_SerializeAsEtchFiles()
        {
            var files = new List<Anvil.Types.GraphQL.EtchFile>
            {
                new EtchCastRef { Id = "template", CastEid = "cast1" },
                new DocumentUpload { Id = "upload", Title = "Upload", File = new Base64Upload { Data = "JVBERi0=", Filename = "doc.pdf" } },
                new DocumentMarkup { Id = "html", Filename = "page.pdf", Markup = new HtmlContent { Html = "<p>Hi</p>" } },
                new DocumentMarkdown { Id = "md", Filename = "notes.pdf", Fields = new List<object> { new MarkdownContent { Content = "Hello" } } },
            };

            var json = Serialize(new CreateEtchPacketPayload { Files = files })["files"]!;

            Assert.Equal("cast1", json[0]!["castEid"]!.ToString());
            Assert.Equal("application/pdf", json[1]!["file"]!["mimetype"]!.ToString());
            Assert.Equal("<p>Hi</p>", json[2]!["markup"]!["html"]!.ToString());
            Assert.Equal("Hello", json[3]!["fields"]![0]!["content"]!.ToString());
        }

        [Fact]
        public void UrlUpload_SendsTheFilenameOnlyWhenGiven()
        {
            var json = Serialize(new DocumentUpload { Id = "upload", File = new UrlUpload { Url = "https://example.com/doc.pdf" } })["file"]!;

            Assert.Equal("https://example.com/doc.pdf", json["url"]!.ToString());
            Assert.Null(json["filename"]);
            Assert.Equal("doc.pdf", Serialize(new UrlUpload { Url = "https://example.com/x", Filename = "doc.pdf" })["filename"]!.ToString());
        }

        [Fact]
        public void SignatureField_SerializesItsRect()
        {
            var json = Serialize(new SignatureField { Id = "sig", Type = "signature", PageNum = 1, Rect = new Rect { X = 10, Y = 20.5, Width = 100, Height = 30 } });

            Assert.Equal(1, (int)json["pageNum"]!);
            Assert.Equal(20.5, (double)json["rect"]!["y"]!);
            Assert.Equal(100, (double)json["rect"]!["width"]!);
        }

        [Fact]
        public void FillPdf_SerializesDataAndOptions()
        {
            var json = Serialize(new FillPdfPayload
            {
                Title = "T",
                FontSize = 10,
                TextColor = "#333",
                UseInteractiveFields = true,
                Data = new Dictionary<string, object> { ["name"] = "Sally", ["date"] = "2026-01-01" },
            });

            Assert.Equal("Sally", json["data"]!["name"]!.ToString());
            Assert.True((bool)json["useInteractiveFields"]!);
            Assert.Equal(10, (int)json["fontSize"]!);
        }

        [Fact]
        public void GeneratePdf_MarkdownAndHtml()
        {
            var markdown = Serialize(new GeneratePdfPayload
            {
                Title = "MD",
                Data = new List<MarkdownContent> { new MarkdownContent { Heading = "H", Content = "c", FontSize = 12 } },
                Logo = new Logo { Src = "https://example.com/logo.png", MaxWidth = 100 },
                IncludeTimestamp = true,
                OutputFormat = GeneratePdfPayload.OutputFormatValues.Url,
            });
            Assert.Equal("H", markdown["data"]![0]!["heading"]!.ToString());
            Assert.Equal(100, (int)markdown["logo"]!["maxWidth"]!);
            Assert.Equal("url", markdown["outputFormat"]!.ToString());

            var html = Serialize(new GeneratePdfPayload { Type = "html", Data = new HtmlContent { Html = "<b>x</b>" }, Logo = false });
            Assert.Equal("<b>x</b>", html["data"]!["html"]!.ToString());
            Assert.False((bool)html["logo"]!);
        }

        [Fact]
        public void ForgeSubmit_SerializesItsVariables()
        {
            var json = Serialize(new ForgeSubmitPayload { ForgeEid = "forge1", Payload = new { a = 1 }, IsTest = true, Complete = true, Timezone = "America/Los_Angeles", WebhookUrl = "https://example.com/hook" });

            Assert.Equal("forge1", json["forgeEid"]!.ToString());
            Assert.Equal(1, (int)json["payload"]!["a"]!);
            Assert.True((bool)json["complete"]!);
            Assert.Equal("https://example.com/hook", json["webhookURL"]!.ToString());
        }
    }

    public class OperationsTests
    {
        [Fact]
        public void Selection_AddsBracesWhenMissing()
        {
            Assert.Equal("{ eid name }", Operations.Selection("eid name"));
            Assert.Equal("{ eid name }", Operations.Selection("  { eid name }  "));
            Assert.Equal("{\n  eid\n}", Operations.Selection("{\n  eid\n}"));
        }

        [Fact]
        public void Operations_UseTheDefaultResponseQueries()
        {
            Assert.Contains(Operations.CreateEtchPacketDefaultResponseQuery, Operations.CreateEtchPacketMutation());
            Assert.Contains(Operations.EtchPacketDefaultResponseQuery, Operations.EtchPacketQuery());
            Assert.Contains(Operations.ForgeSubmitDefaultResponseQuery, Operations.ForgeSubmitMutation());
            Assert.Contains(Operations.CurrentUserDefaultResponseQuery, Operations.CurrentUserQuery());
        }

        [Fact]
        public void Operations_MatchNodeAnvilsVariables()
        {
            var create = Operations.CreateEtchPacketMutation();
            foreach (var variable in new[] { "$name: String", "$organizationEid: String", "$files: [EtchFile!]", "$signers: [JSON!]", "$enableEmails: JSON", "$createCastTemplatesFromUploads: Boolean", "$duplicateCasts: Boolean", "$mergePDFs: Boolean", "$allowUpdates: Boolean" })
            {
                Assert.Contains(variable, create);
            }

            var forge = Operations.ForgeSubmitMutation();
            Assert.Contains("$enforcePayloadValidOnCreate: Boolean", forge);
            Assert.Contains("$webhookURL: String", forge);
            Assert.DoesNotContain("errorType", forge);

            Assert.DoesNotContain("documentGroup {\n    id", Operations.CreateEtchPacketMutation());
            Assert.Contains("documentGroup {\n    eid", Operations.EtchPacketQuery());
        }
    }
}
