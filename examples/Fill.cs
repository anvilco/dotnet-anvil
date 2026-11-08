// Fill a PDF template with data and save the PDF.
// Docs: https://www.useanvil.com/docs/api/fill-pdf
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- fill [pdfTemplateEid] [output.pdf]
using Anvil;
using Anvil.Payloads;

namespace AnvilExamples;

public static class Fill
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        // A sample PDF template available to any account. See
        // https://www.useanvil.com/help/tutorials/set-up-a-pdf-template to
        // set up your own template, and find its eid on its page.
        var pdfTemplateEid = args.Length > 0 ? args[0] : "05xXsZko33JIO6aq5Pnr";
        var outputPath = args.Length > 1 ? args[1] : "fill-output.pdf";

        var payload = new FillPdfPayload
        {
            Title = "My PDF Title",
            TextColor = "#333333",
            // Field ids come from the template's API info page. A field may take
            // an object for structured data, like a phone number or an address.
            Data = new Dictionary<string, object>
            {
                ["shortTextField"] = "Hello World!",
                ["date"] = "2026-01-30",
                ["name"] = new { firstName = "Sally", lastName = "Jones" },
                ["email"] = "sally@example.com",
                ["phone"] = new { num = "5554443333", region = "US", baseRegion = "US" },
                ["usAddress"] = new { street1 = "123 Main St #234", city = "San Francisco", state = "CA", zip = "94106", country = "US" },
                ["checkbox"] = true,
                ["dollar"] = 123.45,
                ["integer"] = 12345,
                ["number"] = 12345.67,
            },
        };

        // Fills the latest published version. Pass AnvilClient.VERSION_LATEST to fill a draft.
        await anvil.FillPdfToFileAsync(pdfTemplateEid, payload, outputPath);
        Console.WriteLine($"Filled {pdfTemplateEid} and saved the PDF to {outputPath}");
    }
}
