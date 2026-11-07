// Create an Etch e-sign packet for an embedded signer, who signs through a URL
// in your own app rather than an email from Anvil, and generate that URL.
// Docs: https://www.useanvil.com/docs/api/e-signatures#controlling-the-signature-process-with-embedded-signers
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- sign-embedded [signer@example.com]
using Anvil;
using Anvil.Payloads;
using Anvil.Types.GraphQL;

namespace AnvilExamples;

public static class SignEmbedded
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        var pdfTemplateEid = "05xXsZko33JIO6aq5Pnr";
        var signerEmail = args.Length > 0 ? args[0] : "testy@example.com";

        var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
        {
            Name = "Embedded signing - Testy Signer",
            IsDraft = false,
            IsTest = true,
            Files = new List<EtchFile> { new EtchCastRef { Id = "templatePdf", CastEid = pdfTemplateEid } },
            Signers = new List<object>
            {
                new EtchSigner
                {
                    Id = "signer1",
                    Name = "Testy Signer",
                    Email = signerEmail,
                    // No email is sent: you generate a signing URL and show it in your app
                    SignerType = EtchSigner.SignerTypeValues.Embedded,
                    // Where to send the signer when they finish
                    RedirectUrl = "https://example.com/signed",
                    Fields = new List<object> { new SignerField { FileId = "templatePdf", FieldId = "signature1" } },
                },
            },
        });

        var signer = packet.DocumentGroup!.Signers!.First()!;
        Console.WriteLine($"Created packet {packet.Eid} with signer {signer.Eid}");

        // The URL is short-lived: generate it when the signer is ready to sign,
        // with the id of the signer in your own system.
        var url = await anvil.GenerateEtchSignUrlAsync(signer.Eid!, clientUserId: "your-user-id-123");
        Console.WriteLine($"Send the signer to: {url}");
    }
}
