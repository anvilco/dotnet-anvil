![Horizontal Lockupblack](https://user-images.githubusercontent.com/293079/169453889-ae211c6c-7634-4ccd-8ca9-8970c2621b6f.png#gh-light-mode-only)
![Horizontal Lockup copywhite](https://user-images.githubusercontent.com/293079/169453892-895f637b-4633-4a14-b997-960c9e17579b.png#gh-dark-mode-only)

# dotnet-anvil

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Anvil) ![GitHub](https://img.shields.io/github/license/anvilco/dotnet-anvil)](https://www.nuget.org/packages/Anvil/)

This is a library that provides an interface to access the [Anvil API](https://www.useanvil.com/developers/) from applications written in .NET languages: C# etc.

[Anvil](https://www.useanvil.com/developers/) provides easy APIs for all things paperwork.

1. [PDF filling API](https://www.useanvil.com/products/pdf-filling-api/) - fill out a PDF template with a web request and structured JSON data.
2. [PDF generation API](https://www.useanvil.com/products/pdf-generation-api/) - send markdown or HTML and Anvil will render it to a PDF.
3. [Etch e-sign with API](https://www.useanvil.com/products/etch/) - customizable, embeddable, e-signature platform with an API to control the signing process end-to-end.
4. [Anvil Workflows (w/ API)](https://www.useanvil.com/products/workflows/) - Webforms + PDF + e-sign with a powerful no-code builder. Easily collect structured data, generate PDFs, and request signatures.

Learn more on our [Anvil developer page](https://www.useanvil.com/developers/). See the [API guide](https://www.useanvil.com/docs) and the [GraphQL reference](https://www.useanvil.com/docs/api/graphql/reference/) for full documentation.

## Installing

[NuGet package page](https://www.nuget.org/packages/Anvil/)

Using the `dotnet` CLI:

```bash
$ dotnet add package Anvil --version 0.6.0
```

Add as a package reference:

```xml
<ItemGroup>
    <PackageReference Include="Anvil" Version="0.6.0" />
</ItemGroup>
```

## Usage

### Create a client instance

```cs
using Anvil;

var anvil = new AnvilClient(new AnvilClientOptions
{
    ApiKey = "API-KEY-FROM-SETTINGS",
    // Or an OAuth access token: AccessToken = "..."
    // Another environment; defaults to production, https://app.useanvil.com
    // BaseUrl = "https://anvil.example.com",
});

// Fill a PDF template
var filled = await anvil.FillPdfAsync("pdf-template-eid", new FillPdfPayload
{
    Data = new Dictionary<string, object> { ["name"] = "Sally Jones" },
});

// Create an e-signature packet
var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
{
    Name = "Please sign",
    Files = new List<EtchFile> { new EtchCastRef { Id = "contract", CastEid = "pdf-template-eid" } },
    Signers = new List<object>
    {
        new EtchSigner { Id = "signer1", Name = "Sally Jones", Email = "sally@example.com",
            Fields = new List<object> { new SignerField { FileId = "contract", FieldId = "signature1" } } },
    },
});

// Any GraphQL query or mutation: returns the response's `data`
var data = await anvil.RequestGraphQLAsync("query { currentUser { eid } }");

// Any REST request: JSON, bytes or a stream
var pdf = await anvil.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/generate-pdf", new
{
    data = new[] { new { content = "Hello" } },
});
```

One client per API key: it is thread-safe, paces requests to the API's rate limit
(learned from the first response's headers, retrying a `429` after its
`Retry-After`), retries `5xx` and connection failures with backoff, and throws
`AnvilClientException` for an error response, with the structured `Errors`,
`StatusCode` and `Headers`.

Every request derives from `BaseUrl`: REST requests go to `{BaseUrl}/api/v1/`,
GraphQL requests to `{BaseUrl}/graphql` and document downloads to
`{BaseUrl}/api/document-group/`.

The methods: `FillPdfAsync`, `GeneratePdfAsync` and `DownloadDocumentsAsync`
(each with `*StreamAsync` and `*ToFileAsync` variants), `CreateEtchPacketAsync`,
`GetEtchPacketAsync`, `GenerateEtchSignUrlAsync`, `ForgeSubmitAsync`,
`RemoveWeldDataAsync`, `GetCurrentUserAsync`, and the generic
`RequestGraphQLAsync` and `RequestRestAsync`. The payload types in
`Anvil.Payloads` (`FillPdfPayload`, `GeneratePdfPayload`,
`CreateEtchPacketPayload`, `EtchSigner`, ...) extend the types generated from the
API's schemas, so every documented option is available with XML docs.

The `RestClient` and `GraphQLClient` of earlier versions still work, as thin
wrappers over `AnvilClient`, and are obsolete:

```cs
using Anvil.Client;

var client = new GraphQLClient(apiKey);
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

First, you will need to have [uploaded a PDF to Anvil](https://useanvil.com/docs/api/fill-pdf#creating-a-pdf-template). You can find the PDF template's ID on your PDF template's page:

![Find your PDF Template ID](./images/template%20eid.webp)

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

//  A version number can also be passed in. This will retrieve a specific
// version of the PDF to be filled if you don't want the current version
// to be used.
// You can also use the constant `ClientConstants.VERSION_LATEST` to fill a PDF
// that has not been published yet. Use this if you'd like to fill out a draft
// version of your template/PDF.
await restClient.FillPdf("your-template-eid", payload, ClientConstants.VERSION_LATEST);

// or specify a version number
await restClient.FillPdf("your-template-eid", payload, 5);

// specify a version number with an output file path
await restClient.FillPdf("your-template-eid", payload, "/tmp/file.pdf", 5);
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

## Development

Build and run the unit tests from the `src` directory:

```bash
cd src
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### Testing against a real environment

`test-client/` is a console app that runs every client method against an Anvil
environment and prints a `PASS`, `FAIL` or `SKIP` for each. Run it on a release
before and after a change to see which behaviors changed. It uses staging by
default, and needs an API key for that environment:

```bash
ANVIL_API_KEY=<your staging API key> dotnet run --project test-client
```

* `ANVIL_BASE_URL` - _optional_ Test another environment instead, e.g. `http://localhost:3000`.
* `ANVIL_ACCESS_TOKEN` - _optional_ An OAuth access token for the same environment, to test Bearer auth.
* `ANVIL_PDF_TEMPLATE_ID` - _optional_ The PDF template to test `FillPdf` with. Defaults to the first template in your organization that has example data.
* `ANVIL_FORGE_EID` - _optional_ The webform (Forge) in a Workflow to test `ForgeSubmit` with. Defaults to the first one in your organization.

Everything it creates is test data. It creates test Etch packets each run, and removes the Workflow submission it creates.

### The generated types

`src/Anvil/Types` is generated from the API specs that ship with `@anvilco/types` (the GraphQL schema and the OpenAPI spec): `Anvil.Types.Rest` has the REST request bodies (`FillPdfRequest`, `GenerateMarkdownPdfRequest`, `EtchSigner`, ...), `Anvil.Types.GraphQL` every type in the GraphQL schema, and `Anvil.Types.Operations` the arguments of every query and mutation (`CreateEtchPacketArgs`, ...). Enums are string properties with a nested `*Values` class of the documented constants, so a value the API adds later is accepted. Unknown JSON members are kept in `AdditionalData`.

```bash
dev/fetch-schema.sh      # bump @anvilco/types to the latest published version
cd dev && npm run generate   # regenerate src/Anvil/Types from its specs
```

`npm run generate` runs offline, and CI fails if the generated files don't match the specs, so run it after `dev/fetch-schema.sh` or after changing `dev/generate-types.mjs`.

The [Sync API Types workflow](.github/workflows/sync-api-types.yml) does both steps whenever anvil-js publishes a new `@anvilco/types` (its release workflow sends an `anvilco-types-published` dispatch here), and weekly as a backstop, and opens a PR when anything changed. It needs a `CI_GITHUB_TOKEN` repository secret: a token that can push branches and open PRs, so the PR gets its CI checks (python-anvil and anvil-js use the same secret name).

### Checking the GraphQL operations against the schema

The API's GraphQL schema and OpenAPI spec come from the
[`@anvilco/types`](https://www.npmjs.com/package/@anvilco/types) npm package, a
dependency of `dev/` that `dev/fetch-schema.sh` bumps (no API key needed). CI
validates every query and mutation the client sends against the schema:

```bash
cd dev
npm ci
npm run check:graphql
```
