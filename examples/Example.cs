using Anvil.Client;
using Anvil.Payloads.Request.Types;
using Anvil.Payloads.Response;

namespace AnvilExample
{
    public class Example
    {
        const string apiKey = "my-api-key";

        static async Task Main(string[] args)
        {
            var client = new GraphQLClient(apiKey);
            var restClient = new RestClient(apiKey);

            await CreateEtchPacketExistingTemplateExample();
        }

        static async Task GraphqlQueryExample()
        {
            var client = new GraphQLClient(apiKey);

            var query = @"
                query currentUser { currentUser { id } } 
            ";

            // `response` will be a `JObject` type and you can get different
            // object fields by using the indexer operator (`[]`). Note that
            // the field names are in the same case as in the query (camelCase)
            // and won't follow the typical C# PascalCase as in the defined
            // types in this library.
            var response = await client.SendQuery(query, null);
        }

        static async Task GetCurrentUserExample()
        {
            var client = new GraphQLClient(apiKey);

            // get the JSON property you want.
            var response = await client.GetCurrentUser();

            // Some things are JObject instances. You can use the indexer
            // operator (`[]`) to get data within that object.
            var userId = response.CurrentUser["id"];
        }

        static async Task GenerateEtchSignUrlExample()
        {
            var client = new GraphQLClient(apiKey);
            var signerEid = "L41py2aJ2mQb40xXmI48   a";
            var clientUserId = "your-systems-signer-id";
            var data = await client.GenerateEtchSignUrl(signerEid, clientUserId);

            var url = data.GenerateEtchSignUrl;
        }

        static async Task CreateEtchPacketNewPdfExample()
        {
            var client = new GraphQLClient(apiKey);

            var path = "path/to/your/file.pdf";

            // Convert the file bytes in into a base64 encoded string.
            // We will use this directly in the payload.
            var fileBytes = File.ReadAllBytes(path);
            var b64File = Convert.ToBase64String(fileBytes);

            // Prepare the file object in the payload
            var payloadFile = new DocumentUpload
            {
                Id = "new-uploaded-file",
                Title = "Please sign this",
                File = new Base64FileUpload
                {
                    Data = b64File,
                    Filename = "uploaded.pdf"
                },
                Fields = new List<SignatureField>
                {
                    new()
                    {
                        // Remember this `Id`. You will assign it to the signer below.
                        Id = "signField1",
                        Type = "signature",
                        PageNum = 0,
                        Rect = new Rect
                        {
                            X = 100,
                            Y = 100,
                            Width = 100,
                            Height = 100,
                        }
                    }
                }
            };

            var files = new List<IEtchPacketAttachable> {payloadFile};

            // Create your signer and also assign it to a signature field 
            // from the above template (the `Fields` property).
            var signer = new EtchSigner
            {
                Id = "custom-signer-id",
                Name = "Morgan Johnson",
                Email = "morgan@example.com",
                Fields = new[]
                {
                    new SignerField
                    {
                        // This field id is from the file payload above.
                        FieldId = "signField1",
                        FileId = "new-uploaded-file",
                    }
                }
            };
            var signers = new List<EtchSigner> {signer};

            // Put it all together.
            var payload = new Anvil.Payloads.Request.CreateEtchPacket
            {
                Name = "Etch packet new file",
                IsDraft = false,
                IsTest = true,
                SignatureEmailBody = "Custom email body text",
                SignatureEmailSubject = "Custom email subject",
                Signers = signers.ToArray(),
                Files = files.ToArray(),
            };

            var response = await client.CreateEtchPacket(payload);
        }

        static async Task CreateEtchPacketExistingTemplateExample()
        {
            var client = new GraphQLClient(apiKey);

            // This is from your "Document Templates" area in your Anvil account.
            var templateId = "template-id-from-anvil";

            // Add one or more files to a List for the upcoming payload.
            var files = new List<IEtchPacketAttachable>
            {
                new EtchCastRef
                {
                    // Remember this `Id`. You will assign it to the signer below.
                    Id = "existingCastReference",
                    CastEid = templateId,
                }
            };

            // Create your signer and also assign it to a signature field 
            // from the above template (the `Fields` property).
            var signer = new EtchSigner
            {
                Id = "custom-signer-id",
                Name = "Morgan Johnson",
                Email = "morgan@example.com",
                Fields = new[]
                {
                    new SignerField
                    {
                        FieldId = "sign1",
                        FileId = "existingCastReference",
                    }
                }
            };
            var signers = new List<EtchSigner> {signer};

            // Put it all together.
            var payload = new Anvil.Payloads.Request.CreateEtchPacket
            {
                Name = "Etch packet name",
                IsDraft = false,
                IsTest = true,
                SignatureEmailBody = "These forms require informationo from your driver's license. Please have that available.",
                SignatureEmailSubject = "Please fill this out",
                ReplyToName = "Jane Doe",
                ReplyToEmail = "jane@example.com",
                Signers = signers.ToArray(),
                Files = files.ToArray(),
            };

            var response = await client.CreateEtchPacket(payload);
        }

        static async Task<object> DownloadDocumentsExample()
        {
            var restClient = new RestClient(apiKey);
            return await restClient.DownloadDocuments("document-group-eid-here");
        }

        static async Task<object> GetEtchPacketExample()
        {
            var client = new GraphQLClient(apiKey);
            return await client.GetEtchPacket("etch-packet-eid-here");
        }

        static async Task<ForgeSubmitPayload> ForgeSubmitExample()
        {
            var client = new GraphQLClient(apiKey);
            var request = new Anvil.Payloads.Request.ForgeSubmit
            {
                ForgeEid = "forge-eid-here",
                Payload = new
                {
                    Name = new
                    {
                        FirstName = "Taylor",
                        LastName = "Jones"
                    },
                    Email = "tjones@example.com"
                }
            };
            return await client.ForgeSubmit(request);
        }

        static async Task<bool> GeneratePdfExampleHtml()
        {
            var restClient = new RestClient(apiKey);

            var payload = new Anvil.Payloads.Request.GeneratePdf
            {
                Title = "My PDFs title",
                Type = "html",
                Data = new GeneratePdfHtml
                {
                    Html = @"<div id='one'>This is some text</div>",
                    Css = @"#one { color: red; }"
                }
            };

            return await restClient.GeneratePdf(payload, "what.pdf");
        }

        static async Task<bool> GeneratePdfExampleData()
        {
            var restClient = new RestClient(apiKey);

            var payload = new Anvil.Payloads.Request.GeneratePdf
            {
                Title = "my new title",
                Type = "markdown",
                Data = new List<IGeneratePdfListable>
                {
                    new GeneratePdfItem {Label = "new thing1", Content = "Content1"},
                    new GeneratePdfItem {Label = "new thing2", Content = "Content2"},
                }
            };

            return await restClient.GeneratePdf(payload, "what.pdf");
        }
    }
}
