// Create an Etch e-sign packet from a PDF template and send it to a signer by email.
// Docs: https://www.useanvil.com/docs/api/e-signatures
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- sign your.real.email@example.com
//
// A signature request email is sent to the address you pass, so use your real
// email address. The new packet also appears in your dashboard's e-sign area.
using Anvil;
using Anvil.Payloads;
using Anvil.Types.GraphQL;

namespace AnvilExamples;

public static class Sign
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Pass the signer's email address: dotnet run --project examples -- sign you@example.com");
        }

        // A sample PDF template available to any account
        var pdfTemplateEid = "05xXsZko33JIO6aq5Pnr";
        var signerEmail = args[0];

        var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
        {
            Name = "Test Docs - Testy Signer",
            // The packet is ready to send: an email goes to the first signer.
            // Use IsDraft = true to review it in the dashboard first.
            IsDraft = false,
            // Test packets use development signatures and do not count toward your billed packets
            IsTest = true,
            SignatureEmailSubject = "Custom email subject",
            SignatureEmailBody = "Custom please sign these documents",
            Files = new List<EtchFile>
            {
                new EtchCastRef { Id = "templatePdf", CastEid = pdfTemplateEid },
            },
            // Data to fill the template with before signing. Keys are the file ids above.
            Data = new
            {
                payloads = new
                {
                    templatePdf = new
                    {
                        data = new { shortTextField = "Hello World!", date = "2026-01-30", email = signerEmail },
                    },
                },
            },
            Signers = new List<object>
            {
                new EtchSigner
                {
                    Id = "signer1",
                    Name = "Testy Signer",
                    Email = signerEmail,
                    // Signs by email, the default. Each signer signs the fields assigned to them.
                    SignerType = EtchSigner.SignerTypeValues.Email,
                    Fields = new List<object>
                    {
                        new SignerField { FileId = "templatePdf", FieldId = "signature1" },
                    },
                },
            },
        });

        Console.WriteLine($"Created packet {packet.Eid}, status {packet.Status}");
        Console.WriteLine($"Details: {packet.DetailsUrl}");
        Console.WriteLine($"A signature request was emailed to {signerEmail}");
    }
}
