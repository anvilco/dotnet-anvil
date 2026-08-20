// Create an Etch e-sign packet via the Anvil API and send it to a signer.
// Docs: https://www.useanvil.com/docs/api/e-signatures
//
// Run it from a project with the Anvil package installed
// (dotnet add package Anvil):
//   ANVIL_API_KEY=yourKey dotnet run -- your.real.email@example.com
//
// A signature request email is sent to the address you pass, so use your real
// email address. The new packet also appears in your dashboard's e-sign area.

using Anvil.Client;
using Anvil.Payloads.Request.Types;

class CreateEtchPacketExample
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");

        var signerName = "Testy Signer";
        var signerEmail = args.Length > 0 ? args[0] : "";

        var payload = new Anvil.Payloads.Request.CreateEtchPacket
        {
            // The packet is ready to send: an email goes to the first signer.
            // Use IsDraft = true to review it in the dashboard first
            IsDraft = false,

            // Test packets use development signatures and do not count toward
            // your billed packets
            IsTest = true,

            Name = "Test Docs - " + signerName,
            SignatureEmailSubject = "Custom email subject",
            SignatureEmailBody = "Custom please sign these documents....",

            Files = new IEtchPacketAttachable[]
            {
                new EtchCastRef()
                {
                    // Your own ID for referencing this file in Data and
                    // Signers below
                    Id = "sampleTemplate",

                    // A sample PDF template available to any account. See
                    // https://www.useanvil.com/help/tutorials/set-up-a-pdf-template
                    // to set up your own template
                    CastEid = "05xXsZko33JIO6aq5Pnr",
                },
            },

            Signers = new EtchSigner[]
            {
                // Signers sign in the order they are specified
                new()
                {
                    Id = "signer1",
                    Name = signerName,
                    Email = signerEmail,
                    SignerType = "email",

                    // The fields this signer clicks through, in this order
                    Fields = new SignerField[]
                    {
                        new()
                        {
                            FileId = "sampleTemplate",
                            FieldId = "signature",
                        },
                    },
                },
            },

            // This data fills the PDF before it is sent to any signers. IDs
            // here match the fields configured on the PDF template
            Data = new
            {
                Payloads = new
                {
                    sampleTemplate = new
                    {
                        data = new Dictionary<string, dynamic>
                        {
                            {"name", signerName},
                            {"email", signerEmail},
                        }
                    },
                },
            },
        };

        var client = new GraphQLClient(apiKey);
        var response = await client.CreateEtchPacket(payload);

        // The response's CreateEtchPacket member is a dynamic object; fields
        // use the API's camelCase names
        Console.WriteLine("Visit the new packet on your dashboard: "
            + response.CreateEtchPacket["detailsURL"]);
    }
}
