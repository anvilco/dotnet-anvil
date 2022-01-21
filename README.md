# dotnet-anvil

[Anvil](https://useanvil.com/) API library for .NET.

![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Anvil)

## Usage

### Create a client instance

```cs
    var apiKey = "API-KEY-FROM-SETTINGS";
    
    // For GraphQL-related queries
    var client = new GraphQLClient(apiKey);
    
    // If you are looking to use the PDF fill or PDF generate endpoints
    // use the REST Client.
    var restClient = new RestClient(apiKey);
```

### Example usage

Also so the example in the `examples/` directory.

```cs
using Anvil.Client;
using Anvil.Payloads.Request.Types;
using Anvil.Payloads.Response;

class Program
{
    const string apiKey = "MY-API-KEY";
    
    static async Task<bool> GeneratePdfExampleData()
    {
        var restClient = new RestClient(apiKey);

        var payload = new Anvil.Payloads.Request.GeneratePdf
        {
            Title = "My Document Title",
            Type = "markdown",
            Data = new List<IGeneratePdfListable>
            {
                new GeneratePdfItem
                {
                    Label = "new thing1",
                    Content = "Content1",
                    FontSize = "20",
                    TextColor = "#CC0000"
                },
                new GeneratePdfItem
                {
                    Label = "new thing2",
                    Content = "Content2"
                },
            }
        };

        return await restClient.GeneratePdf(payload, "/path/for/file/what.pdf");
    }
}
```