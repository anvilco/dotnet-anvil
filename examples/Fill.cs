// Fill a PDF template with your data via the Anvil API.
// Docs: https://www.useanvil.com/docs/api/fill-pdf
//
// Run it from a project with the Anvil package installed
// (dotnet add package Anvil):
//   ANVIL_API_KEY=yourKey dotnet run

using Anvil.Client;

class FillExample
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");

        // A sample PDF template available to any account. See
        // https://www.useanvil.com/help/tutorials/set-up-a-pdf-template to
        // set up your own template.
        var pdfTemplateEid = "05xXsZko33JIO6aq5Pnr";

        var payload = new Anvil.Payloads.Request.FillPdf
        {
            Title = "My PDF Title",
            FontSize = 10,
            TextColor = "#333333",
            // Keys here match the field IDs configured on the PDF template
            Data = new Dictionary<string, dynamic>()
            {
                {"shortText", "Hello World!"},
                {"date", "2024-01-15"},
                {
                    "name", new Dictionary<string, object>()
                    {
                        {"firstName", "Robin"},
                        {"mi", "W"},
                        {"lastName", "Smith"}
                    }
                },
                {"email", "testy@example.com"},
                {
                    "usAddress", new Dictionary<string, object>()
                    {
                        {"street1", "123 Main St #234"},
                        {"city", "San Francisco"},
                        {"state", "CA"},
                        {"zip", "94106"},
                        {"country", "US"}
                    }
                },
                {"ssn", "456454567"},
                {"ein", "897654321"},
                {"checkbox", true},
                {"decimalNumber", 12345.67},
                {"dollar", 123.45},
                {"integer", 12345},
                {"percent", 50.3},
                {"longText", "Lorem ipsum dolor sit amet, consectetur adipiscing elit."}
            }
        };

        var client = new RestClient(apiKey);
        var wasWritten = await client.FillPdf(pdfTemplateEid, payload, "./fill-output.pdf");

        Console.WriteLine(wasWritten
            ? "Filled PDF saved to fill-output.pdf"
            : "There was an error filling the PDF");
    }
}
