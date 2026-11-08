using System;
using System.Collections.Generic;
using System.Linq;

using Anvil.Types;
using Anvil.Types.GraphQL;
using Anvil.Types.Operations;
using Anvil.Types.Rest;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Xunit;

namespace AnvilTests.Types
{
    public class GeneratedTypesTests
    {
        // The settings the clients serialize with
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        private static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;

        [Fact]
        public void EveryOperationHasAnArgsClass()
        {
            var argsTypes = typeof(OperationNames).Assembly.GetTypes()
                .Where(t => t.Namespace == "Anvil.Types.Operations" && t.Name.EndsWith("Args"))
                .ToList();
            var names = OperationNames.Queries.Concat(OperationNames.Mutations).ToList();

            Assert.NotEmpty(names);
            Assert.Equal(names.Count, argsTypes.Count);
            foreach (var name in names)
            {
                var type = argsTypes.SingleOrDefault(t => t.Name.Equals(name + "Args", StringComparison.OrdinalIgnoreCase));
                Assert.True(type != null, $"No Args class for the `{name}` operation");
                Assert.True(typeof(GeneratedModel).IsAssignableFrom(type), $"{type!.Name} does not extend GeneratedModel");
            }
        }

        [Fact]
        public void TheClientsOperationsHaveArgsClasses()
        {
            Assert.NotNull(typeof(CreateEtchPacketArgs));
            Assert.NotNull(typeof(EtchPacketArgs));
            Assert.NotNull(typeof(GenerateEtchSignUrlArgs));
            Assert.NotNull(typeof(ForgeSubmitArgs));
            Assert.NotNull(typeof(RemoveWeldDataArgs));
        }

        [Fact]
        public void RestTypes_SerializeCamelCaseAndKeepUnknownMembers()
        {
            var request = Deserialize<FillPdfRequest>(@"{
                ""title"": ""My PDF"",
                ""fontSize"": 10,
                ""data"": { ""name"": ""Sally"" },
                ""addedLater"": ""kept""
            }");

            Assert.Equal("My PDF", request.Title);
            Assert.Equal(10, request.FontSize);
            Assert.Equal("Sally", ((JObject)request.Data!)["name"]!.ToString());
            Assert.Equal("kept", request.AdditionalData!["addedLater"]!.ToString());

            var json = JObject.Parse(Serialize(request));
            Assert.Equal("My PDF", json["title"]!.ToString());
            Assert.Equal(10, (int)json["fontSize"]!);
            Assert.Equal("kept", json["addedLater"]!.ToString());
            Assert.False(json.ContainsKey("textColor"), "null properties are left out");
        }

        [Fact]
        public void RestTypes_UseTheApisPropertyNames()
        {
            var signer = new EtchSigner { Id = "signer1", RedirectUrl = "https://example.com/done" };

            var json = JObject.Parse(Serialize(signer));

            Assert.Equal("https://example.com/done", json["redirectURL"]!.ToString());
            Assert.False(json.ContainsKey("redirectUrl"));
        }

        [Fact]
        public void RestTypes_MarkTheRequiredProperties()
        {
            var id = typeof(EtchSigner).GetProperty(nameof(EtchSigner.Id))!.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().Single();
            var name = typeof(EtchSigner).GetProperty(nameof(EtchSigner.Name))!.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().Single();

            Assert.Equal(Required.Always, id.Required);
            Assert.Equal(Required.Default, name.Required);

            // Which is enforced when reading one back without it
            var ex = Assert.Throws<JsonSerializationException>(() => Deserialize<EtchSigner>(@"{ ""name"": ""No id"" }"));
            Assert.Contains("'id'", ex.Message);
        }

        [Fact]
        public void RestTypes_EnumsAreStringsWithDocumentedValues()
        {
            Assert.Equal("in-person", EtchSigner.SignerTypeValues.InPerson);
            Assert.Equal("embedded", EtchSigner.SignerTypeValues.Embedded);
            Assert.Equal("email", EtchSigner.SignerTypeValues.Email);

            // A value the API adds later is accepted rather than rejected
            var signer = new EtchSigner { Id = "signer1", SignerType = "someFutureType" };
            Assert.Equal("someFutureType", JObject.Parse(Serialize(signer))["signerType"]!.ToString());
        }

        [Fact]
        public void RestTypes_NestedObjectsGetTheirOwnClasses()
        {
            var request = new GenerateHtmlPdfRequest
            {
                Data = new GenerateHtmlPdfRequestData { Html = "<h1>Hi</h1>", Css = "h1 { color: red }" },
                Page = new PageOptions { Width = "8.5in" },
            };

            var json = JObject.Parse(Serialize(request));

            Assert.Equal("<h1>Hi</h1>", json["data"]!["html"]!.ToString());
            Assert.Equal("8.5in", json["page"]!["width"]!.ToString());
        }

        [Fact]
        public void OperationArgs_MarkTheRequiredArguments()
        {
            var forgeEid = typeof(ForgeSubmitArgs).GetProperty(nameof(ForgeSubmitArgs.ForgeEid))!.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().Single();
            var isTest = typeof(ForgeSubmitArgs).GetProperty(nameof(ForgeSubmitArgs.IsTest))!.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().Single();

            Assert.Equal(Required.Always, forgeEid.Required);
            // Has a default in the schema, so it is not required
            Assert.Equal(Required.Default, isTest.Required);

            var json = JObject.Parse(Serialize(new ForgeSubmitArgs { ForgeEid = "forge1", Payload = new { name = "Sally" } }));

            Assert.Equal("forge1", json["forgeEid"]!.ToString());
            Assert.Equal("Sally", json["payload"]!["name"]!.ToString());
            Assert.False(json.ContainsKey("isTest"));
        }

        [Fact]
        public void OperationArgs_UseInputTypesFromTheSchema()
        {
            var args = new CreateEtchPacketArgs
            {
                Name = "Packet",
                Files = new List<EtchFile> { new EtchFile { Id = "file1", CastEid = "cast1" } },
                Signers = new List<object> { new { id = "signer1", name = "Sally" } },
            };

            var json = JObject.Parse(Serialize(args));

            Assert.Equal("cast1", json["files"]![0]!["castEid"]!.ToString());
            Assert.Equal("Sally", json["signers"]![0]!["name"]!.ToString());
        }

        [Fact]
        public void GraphQLTypes_DeserializeAResponse()
        {
            var packet = Deserialize<EtchPacket>(@"{
                ""eid"": ""packet1"",
                ""name"": ""Packet"",
                ""status"": ""sent"",
                ""detailsURL"": ""https://app.useanvil.com/packet1"",
                ""documentGroup"": {
                    ""eid"": ""group1"",
                    ""status"": ""sent"",
                    ""signers"": [{ ""eid"": ""signer1"", ""routingOrder"": 1, ""name"": ""Sally"" }]
                }
            }");

            Assert.Equal("packet1", packet.Eid);
            Assert.Equal("https://app.useanvil.com/packet1", packet.DetailsUrl);
            Assert.Equal("group1", packet.DocumentGroup!.Eid);
            var signer = Assert.Single(packet.DocumentGroup.Signers!);
            Assert.Equal("Sally", signer!.Name);
            Assert.Equal(1, signer.RoutingOrder);
        }

        [Fact]
        public void GraphQLTypes_JsonScalarsAreJTokens()
        {
            var submission = Deserialize<Submission>(@"{ ""eid"": ""sub1"", ""currentStep"": 2, ""payloadValue"": { ""a"": 1 } }");

            Assert.Equal(2, submission.CurrentStep);
            Assert.Equal(1, (int)submission.PayloadValue!["a"]!);
        }

        // The namespaces a project with ImplicitUsings imports, as `dotnet new console` does
        private static readonly string[] ImplicitUsings =
        {
            "System", "System.IO", "System.Collections.Generic", "System.Linq",
            "System.Net.Http", "System.Threading", "System.Threading.Tasks",
        };

        [Fact]
        public void NoPublicTypeClashesWithATypeInAnImplicitUsingsNamespace()
        {
            // A clash makes the name ambiguous (CS0104) in any file that imports
            // the Anvil namespace too; the generator renames such GraphQL types
            // in GRAPHQL_RENAMES (dev/generate-types.mjs).
            var bcl = new[] { typeof(object), typeof(System.IO.File), typeof(Enumerable), typeof(System.Net.Http.HttpClient), typeof(List<>), typeof(System.Threading.Thread) }
                .Select(t => t.Assembly)
                .Distinct()
                .SelectMany(a => a.GetExportedTypes())
                .Where(t => ImplicitUsings.Contains(t.Namespace))
                .Select(t => t.Name.Split('`')[0])
                .ToHashSet();

            var clashes = typeof(Anvil.AnvilClient).Assembly.GetExportedTypes()
                .Where(t => !t.IsNested && bcl.Contains(t.Name.Split('`')[0]))
                .Select(t => t.FullName)
                .ToList();

            Assert.Empty(clashes);
        }

        [Fact]
        public void TheSchemasFileTypeIsAnvilFile()
        {
            Assert.Null(typeof(GeneratedModel).Assembly.GetType("Anvil.Types.GraphQL.File"));
            var file = Deserialize<AnvilFile>(@"{""name"":""a.pdf"",""mimetype"":""application/pdf"",""src"":""https://x/a.pdf""}");
            Assert.Equal("a.pdf", file.Name);
            Assert.Equal("application/pdf", file.Mimetype);
        }
    }
}
