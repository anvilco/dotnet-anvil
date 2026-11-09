// Create an Etch e-sign packet from a PDF uploaded with the request, placing
// signature fields on it, and send it to a signer by email.
// Docs: https://www.useanvil.com/docs/api/e-signatures#uploading-a-new-pdf
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- sign-upload your.real.email@example.com /path/to/file.pdf
using Anvil;
using Anvil.Payloads;
using Anvil.Types.GraphQL;

namespace AnvilExamples;

public static class SignUpload
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Pass the signer's email and a PDF: dotnet run --project examples -- sign-upload you@example.com file.pdf");
        }

        var signerEmail = args[0];
        var pdfPath = args[1];

        var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
        {
            Name = "Uploaded PDF - Testy Signer",
            IsDraft = false,
            IsTest = true,
            Files = new List<EtchFile>
            {
                new DocumentUpload
                {
                    Id = "uploadedPdf",
                    Title = "Please sign this document",
                    // Sent as a multipart upload. For a small file, a Base64Upload works too, and for a file at a URL, a UrlUpload.
                    File = AnvilClient.PrepareGraphQLFile(pdfPath),
                    // Where the signer signs: boxes on the first page, in points from the top left
                    Fields = new List<object>
                    {
                        new SignatureField
                        {
                            Id = "signature1",
                            Type = SignatureField.TypeValues.Signature,
                            PageNum = 0,
                            Rect = new Rect { X = 400, Y = 100, Width = 150, Height = 30 },
                        },
                        new SignatureField
                        {
                            Id = "signatureDate1",
                            Type = SignatureField.TypeValues.SignatureDate,
                            PageNum = 0,
                            Rect = new Rect { X = 400, Y = 140, Width = 150, Height = 30 },
                        },
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
                    Fields = new List<object>
                    {
                        new SignerField { FileId = "uploadedPdf", FieldId = "signature1" },
                        new SignerField { FileId = "uploadedPdf", FieldId = "signatureDate1" },
                    },
                },
            },
        });

        Console.WriteLine($"Created packet {packet.Eid} from {pdfPath}: {packet.DetailsUrl}");
        Console.WriteLine($"A signature request was emailed to {signerEmail}");
    }
}
