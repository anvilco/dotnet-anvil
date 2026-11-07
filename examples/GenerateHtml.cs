// Generate a PDF from HTML and CSS and save it.
// Docs: https://www.useanvil.com/docs/api/generate-pdf
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- generate-html [output.pdf]
using Anvil;
using Anvil.Payloads;
using Anvil.Types.Rest;

namespace AnvilExamples;

public static class GenerateHtml
{
    public static async Task Run(AnvilClient anvil, string[] args)
    {
        var outputPath = args.Length > 0 ? args[0] : "generate-html-output.pdf";

        var payload = new GeneratePdfPayload
        {
            Title = "Example Invoice",
            Type = GeneratePdfPayload.TypeValues.Html,
            Data = new HtmlContent
            {
                Html = @"
                    <h1 class='header-one'>What is Lorem Ipsum?</h1>
                    <p>
                      Lorem Ipsum is simply dummy text of the printing and typesetting
                      industry. Lorem Ipsum has been the industry's standard dummy text
                      ever since the <strong>1500s</strong>.
                    </p>
                    <h3 class='header-two'>Where does it come from?</h3>
                    <p>Contrary to popular belief, Lorem Ipsum is not simply random text.</p>",
                Css = @"
                    body { font-size: 14px; color: #171717; }
                    .header-one { text-decoration: underline; }
                    .header-two { font-style: underline; }",
            },
            Page = new PageOptions { Width = "8.5in", Height = "11in", Margin = "60px" },
        };

        await anvil.GeneratePdfToFileAsync(payload, outputPath);
        Console.WriteLine($"Generated a PDF from HTML and saved it to {outputPath}");
    }
}
