// Generate a PDF from Markdown and save it.
// Docs: https://www.useanvil.com/docs/api/generate-pdf
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- generate-markdown [output.pdf]
using Anvil;
using Anvil.Payloads;

namespace AnvilExamples;

public static class GenerateMarkdown
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        var outputPath = args.Length > 0 ? args[0] : "generate-markdown-output.pdf";

        var payload = new GeneratePdfPayload
        {
            Title = "Example Invoice",
            // Markdown is the default type
            Data = new List<MarkdownContent>
            {
                new() { Label = "Name", Content = "Sally Jones" },
                new() { Content = "Lorem **ipsum** dolor sit _amet_, consectetur adipiscing elit." },
                new()
                {
                    Table = new MarkdownTable
                    {
                        FirstRowHeaders = true,
                        Rows = new List<List<string?>?>
                        {
                            new() { "Description", "Quantity", "Price" },
                            new() { "4x Large Widgets", "4", "$40.00" },
                            new() { "10x Medium Sized Widgets in dark blue", "10", "$100.00" },
                            new() { "10x Small Widgets in white", "6", "$60.00" },
                        },
                    },
                },
            },
        };

        await anvil.GeneratePdfToFileAsync(payload, outputPath);
        Console.WriteLine($"Generated a PDF from Markdown and saved it to {outputPath}");
    }
}
