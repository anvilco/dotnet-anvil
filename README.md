# dotnet-anvil

[Anvil](https://useanvil.com/) API library for .NET.


[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Anvil) ![GitHub](https://img.shields.io/github/license/anvilco/dotnet-anvil)](https://www.nuget.org/packages/Anvil/)


## Installing 

[NuGet package page](https://www.nuget.org/packages/Anvil/)

Using the `dotnet` CLI:

```bash
$ dotnet add package Anvil --version 0.1.0-alpha2
```

Add as a package reference:
```xml
<ItemGroup>
    <PackageReference Include="Anvil" Version="0.1.0-alpha2" />
</ItemGroup>
```

## Usage

### Create a client instance

```cs
using Anvil.Client;

var apiKey = "API-KEY-FROM-SETTINGS";

// For GraphQL-related queries
var client = new GraphQLClient(apiKey);

// If you are looking to use the PDF fill or PDF generate endpoints
// use the REST Client.
var restClient = new RestClient(apiKey);
```

### Example usage

Also see [the example in the `examples/` directory](./examples/Example.cs).

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