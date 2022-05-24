# dotnet-anvil

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Anvil) ![GitHub](https://img.shields.io/github/license/anvilco/dotnet-anvil)](https://www.nuget.org/packages/Anvil/)

[Anvil](https://www.useanvil.com/) API library for .NET. Anvil is a suite of tools for managing document-based workflows.

**Repository maintained by [Anvil](www.useanvil.com/developers)**

![Horizontal Lockupblack](https://user-images.githubusercontent.com/293079/169453889-ae211c6c-7634-4ccd-8ca9-8970c2621b6f.png#gh-light-mode-only)
![Horizontal Lockup copywhite](https://user-images.githubusercontent.com/293079/169453892-895f637b-4633-4a14-b997-960c9e17579b.png#gh-dark-mode-only)

Anvil provides easy APIs for all things paperwork.
1. [PDF filling API](https://www.useanvil.com/products/pdf-filling-api/) - fill out a PDF template with a web request and structured JSON data.
2. [PDF generation API](https://www.useanvil.com/products/pdf-generation-api/) - send markdown or HTML and Anvil will render it to a PDF.
3. [Etch E-sign with API](https://www.useanvil.com/products/etch/) - customizable, embeddable, e-signature platform with an API to control the signing process end-to-end.
4. [Anvil Workflows (w/ API)](https://www.useanvil.com/products/workflows/) - Webforms + PDF + E-sign with a powerful no-code builder. Easily collect structured data, generate PDFs, and request signatures.

Learn more on our [Anvil developer page](www.useanvil.com/developers).

---

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

## API

- [RestClient.FillPdf](#restclientfillpdf)
- [RestClient.GeneratePdf](#restclientgeneratepdf)
- [RestClient.DownloadDocuments](#restclientdownloaddocuments)
- [GraphQLClient.CreateEtchPacket](#graphqlclientcreateetchpacket)
- [GraphQLClient.GetEtchPacket](#graphqlclientgetetchpacket)
- [GraphQLClient.GetEtchSignUrl](#graphqlclientgetetchsignurl)
- [GraphQLClient.DownloadDocuments](#graphqlclientgetetchsignurl)
- [GraphQLClient.SendQuery](#graphqlclientsendquery)

### RestClient.FillPdf

Fills a PDF template with your JSON data.

First, you will need to have [uploaded a PDF to Anvil](https://useanvil.com/docs/api/fill-pdf#creating-a-pdf-template). You can find the PDF template's id on the `API Info` tab of your PDF template's page:

<img width="725" alt="pdf-template-id" src="https://user-images.githubusercontent.com/69169/73693549-4a598280-468b-11ea-81a3-5df4472de8a4.png">

An example:

```cs
var restClient = new RestClient(apiKey);

// Use Payload.Request objects to create your API call.
var payload = new Anvil.Payloads.Request.FillPdf
{
    Title = "My PDF Title",
    TextColor = "#CC0000",
    Data = new Dictionary<string, object>
    {
        { "simpleTextField", "string data" },
        // Some fields are JSON objects, use an object initializer for those.
        // In your PDF template's API info page, you can see if your field has
        // additional data.
        { "phone", new {
            Num = "5551231234",
            Region = "US"
        } },
    }
};

// This will return a `Stream`
await restClient.FillPdf("your-template-eid", payload);

// This will write directly to the path you specify
await restClient.FillPdf("your-template-eid", payload, "/tmp/file.pdf");
```

### RestClient.GeneratePdf

Dynamically generate a new PDF from your HTML and CSS or markdown.

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

### RestClient.DownloadDocuments

Returns a `Stream` of the document group specified by the documentGroupEid in Zip file format.

```cs
var restClient = new RestClient(apiKey);
return await restClient.DownloadDocuments("document-group-eid-here");
```

### GraphQLClient.CreateEtchPacket

Creates an Etch Packet and optionally sends it to the first signer.

```cs
var client = new GraphQLClient(apiKey);

// This is from your "Document Templates" area in your Anvil account.
var templateId = "template-id-from-anvil";

// Add one or more files to a List for the upcoming payload.
// The "files" here are existing Casts in your Anvil account.
var files = new List<IEtchPacketAttachable>
{
    new EtchCastRef
    {
        // Remember this `Id`. You will assign it to the signer below.
        Id = "existingCastReference",
        CastEid = templateId,
    }
};

// Create your signer and also assign it to a signature field
// from the above template (the `Fields` property).
var signer = new EtchSigner
{
    Id = "custom-signer-id",
    Name = "Morgan Johnson",
    Email = "morgan@example.com",
    Fields = new[]
    {
        new SignerField
        {
            FieldId = "sign1",
            FileId = "existingCastReference",
        }
    }
};
var signers = new List<EtchSigner> { signer };

// Put it all together.
var payload = new Anvil.Payloads.Request.CreateEtchPacket
{
    Name = "Etch packet name",
    IsDraft = false,
    IsTest = true,
    SignatureEmailBody = "Please sign this!",
    SignatureEmailSubject = "Your signature is required",
    Signers = signers.ToArray(),
    Files = files.ToArray(),
    // If you'd like all your PDFs merged into one when signing, and in the resulting zip package.
    // Defaults to `false`.
    // MergePdfs = true,
};

var response = await client.CreateEtchPacket(payload);
```

### GraphQLClient.GetEtchPacket

Gets the details of an Etch Packet.

```cs
var client = new GraphQLClient(apiKey);
return await client.GetEtchPacket("etch-packet-eid-here");
```

### GraphQLClient.GetEtchSignUrl

Generates an Etch sign URL for an Etch Packet signer. The Etch Packet and its signers must have already been created.

```cs
var client = new GraphQLClient(apiKey);
var signerEid = "some-id-here-from-etch-packet";
var clientUserId = "your-systems-signer-id";
var data = await client.GenerateEtchSignUrl(signerEid, clientUserId);

// The URL for signing is here
var url = data.GenerateEtchSignUrl;
```

### GraphQLClient.SendQuery

A fallback function for queries and mutations without a specialized function in this client.

See the [GraphQL reference](https://www.useanvil.com/docs/api/graphql/reference/) for a listing on all possible queries.

```cs
var client = new GraphQLClient(apiKey);

var query = @"
    query currentUser { currentUser { id } }
";

// `response` will be a `JObject` type and you can get different
// object fields by using the indexer operator (`[]`). Note that
// the field names are in the same case as in the query (camelCase)
// and won't follow the typical C# PascalCase as in the defined
// types in this library.
var response = await client.SendQuery(query, null);
```
