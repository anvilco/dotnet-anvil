// Generate a PDF from Markdown-structured data via the Anvil API.
// Docs: https://www.useanvil.com/docs/api/generate-pdf#markdown-to-pdf
//
// Run it from a project with the Anvil package installed
// (dotnet add package Anvil):
//   ANVIL_API_KEY=yourKey dotnet run

using Anvil.Client;
using Anvil.Payloads.Request;
using Anvil.Payloads.Request.Types;

class GenerateMarkdownExample
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");

        var payload = new GeneratePdf()
        {
            Title = "Example Invoice",
            Data = new List<IGeneratePdfListable>
            {
                new GeneratePdfItem()
                {
                    Label = "Name",
                    Content = "Sally Jones",
                },
                new GeneratePdfItem()
                {
                    Content = @"
Lorem **ipsum** dolor sit _amet_, consectetur adipiscing elit, sed [do eiusmod](https://www.useanvil.com/docs) tempor incididunt ut labore et dolore magna aliqua.

* Sagittis eu volutpat odio facilisis.

* Erat nam at lectus urna.",
                },
                new GeneratePdfTable()
                {
                    Table = new GeneratePdfTableContent()
                    {
                        FirstRowHeaders = true,
                        Rows = new List<List<string>>()
                        {
                            new() { "Description", "Quantity", "Price" },
                            new() { "4x Large Widgets", "4", "$40.00" },
                            new() { "10x Medium Sized Widgets in dark blue", "10", "$100.00" },
                            new() { "10x Small Widgets in white", "6", "$60.00" }
                        }
                    },
                },
            }
        };

        var client = new RestClient(apiKey);
        var wasWritten = await client.GeneratePdf(payload, "./generate-markdown-output.pdf");

        Console.WriteLine(wasWritten
            ? "Generated PDF saved to generate-markdown-output.pdf"
            : "There was an error generating the PDF");
    }
}
