// Generate a PDF from HTML and CSS via the Anvil API.
// Docs: https://www.useanvil.com/docs/api/generate-pdf#html--css-to-pdf
//
// Run it from a project with the Anvil package installed
// (dotnet add package Anvil):
//   ANVIL_API_KEY=yourKey dotnet run

using Anvil.Client;
using Anvil.Payloads.Request;
using Anvil.Payloads.Request.Types;

class GenerateHtmlExample
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");

        var payload = new GeneratePdf()
        {
            Title = "Example HTML to PDF",
            Type = "html",
            Data = new GeneratePdfHtml()
            {
                Html = @"
                    <h1 class='header-one'>What is Lorem Ipsum?</h1>
                    <p>
                      Lorem Ipsum is simply dummy text of the printing and typesetting
                      industry. Lorem Ipsum has been the industry's standard dummy text
                      ever since the <strong>1500s</strong>, when an unknown printer took
                      a galley of type and scrambled it to make a type specimen book.
                    </p>
                    <h3 class='header-two'>Where does it come from?</h3>
                    <p>
                      Contrary to popular belief, Lorem Ipsum is not simply random text.
                      It has roots in a piece of classical Latin literature from
                      <i>45 BC</i>, making it over <strong>2000</strong> years old.
                    </p>
                ",
                Css = @"
                    body { font-size: 14px; color: #171717; }
                    .header-one { text-decoration: underline; }
                    .header-two { font-style: italic; }
                ",
            }
        };

        var client = new RestClient(apiKey);
        var wasWritten = await client.GeneratePdf(payload, "./generate-html-output.pdf");

        Console.WriteLine(wasWritten
            ? "Generated PDF saved to generate-html-output.pdf"
            : "There was an error generating the PDF");
    }
}
