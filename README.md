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

## Usage

Install the [`Anvil` package](https://www.nuget.org/packages/Anvil/) from NuGet. It targets .NET Standard 2.0 (.NET Core 2.0+, .NET 5+ or .NET Framework 4.6.1+) and .NET 10.

```bash
dotnet add package Anvil
```

Create a client with your API key, from your organization's API settings, and call the methods. Everything is async.

```cs
using Anvil;
using Anvil.Payloads;

var anvil = new AnvilClient(new AnvilClientOptions { ApiKey = "API-KEY-FROM-SETTINGS" });

// Fill a PDF template with data: the PDF's bytes
var pdf = await anvil.FillPdfAsync("pdf-template-eid", new FillPdfPayload
{
    Data = new Dictionary<string, object>
    {
        ["name"] = "Sally Jones",
        ["date"] = "2026-01-30",
    },
});
await File.WriteAllBytesAsync("filled.pdf", pdf);
```

One `AnvilClient` per API key: it is thread-safe, and paces requests to the API's [rate limit](#rate-limits). Dispose it when your app is done with it.

### Types

The payload types in `Anvil.Payloads` (`FillPdfPayload`, `GeneratePdfPayload`, `CreateEtchPacketPayload`, `EtchSigner`, ...) extend types generated from the API's own OpenAPI spec and GraphQL schema (`Anvil.Types`), so every option the API documents is a property with XML docs, and enum values are constants like `EtchSigner.SignerTypeValues.Embedded`. A value the API adds later is accepted, and any JSON members a type doesn't declare are kept in `AdditionalData`. Every method also takes its payload as an anonymous object, a dictionary, a `JObject` or a JSON string with the same shape, and the GraphQL methods return the generated response types (`EtchPacket`, `Submission`, ...).

## API

- [Instance methods](#instance-methods)
  - [new AnvilClient(options)](#new-anvilclientoptions)
  - [FillPdfAsync](#fillpdfasync)
  - [GeneratePdfAsync](#generatepdfasync)
  - [CreateEtchPacketAsync](#createetchpacketasync)
  - [GetEtchPacketAsync](#getetchpacketasync)
  - [GenerateEtchSignUrlAsync](#generateetchsignurlasync)
  - [DownloadDocumentsAsync](#downloaddocumentsasync)
  - [ForgeSubmitAsync](#forgesubmitasync)
  - [RemoveWeldDataAsync](#removeweldataasync)
  - [RequestGraphQLAsync](#requestgraphqlasync)
  - [RequestRestAsync](#requestrestasync)
- [Static methods](#static-methods)
  - [PrepareGraphQLFile](#preparegraphqlfile)
- [Options](#options)
  - [Authentication](#authentication)
  - [BaseUrl](#baseurl)
- [Constants](#constants)
- [Errors](#errors)
- [Rate limits](#rate-limits)
- [Retries](#retries)

### Instance methods

#### new AnvilClient(options)

```cs
var anvil = new AnvilClient(new AnvilClientOptions
{
    ApiKey = "...",                             // or AccessToken = "..."
    BaseUrl = "https://app.useanvil.com",       // the default
});

// Or, with the defaults
var anvil = new AnvilClient("API-KEY");
```

See [Options](#options) for everything it takes.

#### FillPdfAsync

Fills a PDF template with your JSON data and returns the PDF. See the [PDF filling guide](https://www.useanvil.com/docs/api/fill-pdf).

First, you will need to have [uploaded a PDF to Anvil](https://useanvil.com/docs/api/fill-pdf#creating-a-pdf-template). You can find the PDF template's ID on your PDF template's page:

![Find your PDF Template ID](./images/template%20eid.webp)

```cs
var payload = new FillPdfPayload
{
    Title = "My PDF Title",
    TextColor = "#CC0000",
    Data = new Dictionary<string, object>
    {
        { "simpleTextField", "string data" },
        // Some fields take an object. In your PDF template's API info page, you
        // can see if your field has additional data.
        { "phone", new { num = "5551231234", region = "US" } },
    },
};

// The PDF's bytes
byte[] pdf = await anvil.FillPdfAsync("pdf-template-eid", payload);

// Or a stream, to read a large PDF without holding it in memory
using Stream stream = await anvil.FillPdfStreamAsync("pdf-template-eid", payload);

// Or straight to a file
await anvil.FillPdfToFileAsync("pdf-template-eid", payload, "/tmp/filled.pdf");

// A version of the template to fill. When left out, the latest published
// version is used, or the latest draft if the template has never been published.
await anvil.FillPdfAsync("pdf-template-eid", payload, AnvilClient.VERSION_LATEST); // the latest draft
await anvil.FillPdfAsync("pdf-template-eid", payload, 5);                          // version 5
```

`FillPdfPayload` has every option the API documents: `FontSize`, `FontFamily`, `TextColor`, `UseInteractiveFields`, `OutputFormat` (`"url"` for a link instead of the PDF; the response is then the JSON's bytes) and more.

#### GeneratePdfAsync

Dynamically generates a new PDF from your HTML and CSS or markdown and returns it. See the [PDF generation guide](https://www.useanvil.com/docs/api/generate-pdf).

```cs
// Markdown (the default type)
var payload = new GeneratePdfPayload
{
    Title = "My Document Title",
    Data = new List<MarkdownContent>
    {
        new() { Label = "Name", Content = "Sally Jones" },
        new() { Content = "Lorem **ipsum** dolor sit _amet_." },
        new()
        {
            Table = new MarkdownTable
            {
                FirstRowHeaders = true,
                Rows = new List<List<string?>?>
                {
                    new() { "Description", "Quantity", "Price" },
                    new() { "4x Large Widgets", "4", "$40.00" },
                },
            },
        },
    },
};

// HTML and CSS
var payload = new GeneratePdfPayload
{
    Title = "My Document Title",
    Type = GeneratePdfPayload.TypeValues.Html,
    Data = new HtmlContent
    {
        Html = "<h1>Hello</h1><p>From the Anvil .NET client</p>",
        Css = "h1 { color: #4c5bf1; }",
    },
    Page = new PageOptions { Width = "8.5in", Height = "11in", Margin = "60px" },
};

byte[] pdf = await anvil.GeneratePdfAsync(payload);
using Stream stream = await anvil.GeneratePdfStreamAsync(payload);
await anvil.GeneratePdfToFileAsync(payload, "/tmp/generated.pdf");
```

#### CreateEtchPacketAsync

Creates an Etch packet: the documents to sign, the signers, and how they sign. Unless it is a draft, the packet is sent to its first signer. See the [e-signature guide](https://www.useanvil.com/docs/api/e-signatures).

```cs
using Anvil.Types.GraphQL;

var payload = new CreateEtchPacketPayload
{
    Name = "Etch packet name",
    // Defaults: sent right away (IsDraft = false), as a test packet (IsTest = true)
    IsDraft = false,
    IsTest = true,
    SignatureEmailSubject = "Your signature is required",
    SignatureEmailBody = "Please sign this!",
    // Files: PDF templates from your account, uploaded PDFs, or PDFs generated
    // from HTML or Markdown. The Id is what signers' fields refer to.
    Files = new List<EtchFile>
    {
        new EtchCastRef { Id = "contract", CastEid = "pdf-template-eid" },
        new DocumentUpload
        {
            Id = "upload",
            Title = "Please sign this too",
            File = AnvilClient.PrepareGraphQLFile("/path/to/file.pdf"),   // or a Base64Upload
            Fields = new List<object>
            {
                new SignatureField { Id = "sig", Type = "signature", PageNum = 0, Rect = new Rect { X = 400, Y = 100, Width = 150, Height = 30 } },
            },
        },
    },
    // Data to fill the templates with before signing, keyed by file id
    Data = new { payloads = new { contract = new { data = new { name = "Sally Jones" } } } },
    Signers = new List<object>
    {
        new EtchSigner
        {
            Id = "signer1",
            Name = "Sally Jones",
            Email = "sally@example.com",
            // "email" (the default), "embedded" or "in-person"
            SignerType = EtchSigner.SignerTypeValues.Email,
            Fields = new List<object>
            {
                new SignerField { FileId = "contract", FieldId = "signature1" },
                new SignerField { FileId = "upload", FieldId = "sig" },
            },
        },
    },
    // If you'd like all the PDFs merged into one when signing, and in the resulting zip
    // MergePdfs = true,
    WebhookUrl = "https://example.com/anvil-webhook",
};

EtchPacket packet = await anvil.CreateEtchPacketAsync(payload);
Console.WriteLine($"{packet.Eid} {packet.Status} {packet.DetailsUrl}");
var signerEid = packet.DocumentGroup.Signers[0].Eid;
```

`EtchSigner` has every signer option the API documents: `RoutingOrder`, `RedirectUrl`, `EnableEmails`, `RequireIdCheck`, `SignatureExperience`, `TokenValidForMinutes`, `AccessCode` and more. `CreateEtchPacketPayload` has every argument of the `createEtchPacket` mutation: `SignaturePageOptions`, `ReplyToName`, `ReplyToEmail`, `CreateCastTemplatesFromUploads`, `DuplicateCasts`, `AllowUpdates`, `OrganizationEid` and more.

The response has the fields in `Operations.CreateEtchPacketDefaultResponseQuery`. To select others, pass a `responseQuery`, with or without the braces; fields the `EtchPacket` type doesn't have are in `AdditionalData`. To send your own mutation document, pass `mutation`.

```cs
var packet = await anvil.CreateEtchPacketAsync(payload, responseQuery: "eid status documentGroup { eid signers { eid } }");
```

#### GetEtchPacketAsync

Gets an Etch packet: its status, documents and signers.

```cs
EtchPacket packet = await anvil.GetEtchPacketAsync("etch-packet-eid");
Console.WriteLine($"{packet.Status}, {packet.NumberRemainingSigners} signers remaining");

// With a response query
var packet = await anvil.GetEtchPacketAsync("etch-packet-eid", responseQuery: "{ eid status }");
```

#### GenerateEtchSignUrlAsync

Generates the URL an `embedded` signer signs at. The packet and its signers must have already been created. See [embedded signers](https://www.useanvil.com/docs/api/e-signatures#controlling-the-signature-process-with-embedded-signers).

```cs
// The signer's eid comes from the packet's DocumentGroup.Signers; the client
// user id is the signer's id in your system
string url = await anvil.GenerateEtchSignUrlAsync(signerEid, clientUserId: "your-user-id-123");
```

#### DownloadDocumentsAsync

Downloads the documents of an Etch packet's document group as a zip. See [downloading documents](https://www.useanvil.com/docs/api/e-signatures#downloading-documents).

```cs
byte[] zip = await anvil.DownloadDocumentsAsync(packet.DocumentGroup.Eid);
using Stream stream = await anvil.DownloadDocumentsStreamAsync(packet.DocumentGroup.Eid);
await anvil.DownloadDocumentsToFileAsync(packet.DocumentGroup.Eid, "/tmp/documents.zip");
```

#### ForgeSubmitAsync

Submits data to a Workflow's webform (`Forge`), creating or updating a submission. See the [`forgeSubmit` reference](https://www.useanvil.com/docs/api/graphql/reference/#operation-forgesubmit-Mutations).

```cs
using Anvil.Types.GraphQL;

Submission submission = await anvil.ForgeSubmitAsync(new ForgeSubmitPayload
{
    ForgeEid = "forge-eid",
    Payload = new { name = "Sally Jones", email = "sally@example.com" },
    IsTest = true,
    // To update an existing submission
    // WeldDataEid = "...", SubmissionEid = "...",
});
Console.WriteLine($"{submission.Eid} {submission.Status} {submission.ContinueUrl} {submission.WeldData.Eid}");
```

Takes `responseQuery` and `mutation` like `CreateEtchPacketAsync`.

#### RemoveWeldDataAsync

Removes a Workflow submission's data (`WeldData`). Returns whether it was removed.

```cs
bool removed = await anvil.RemoveWeldDataAsync(submission.WeldData.Eid);
```

#### RequestGraphQLAsync

Sends any GraphQL query or mutation and returns the response's `data`. See the [GraphQL reference](https://www.useanvil.com/docs/api/graphql/reference/) for everything the API has.

```cs
using Newtonsoft.Json.Linq;

JObject data = await anvil.RequestGraphQLAsync(@"
    query CurrentUser {
      currentUser { eid name email }
    }");
Console.WriteLine(data["currentUser"]["email"]);

// With variables: an anonymous object, a dictionary, a JObject or a payload type
var data = await anvil.RequestGraphQLAsync(
    "query Cast($eid: String!) { cast(eid: $eid) { eid title } }",
    new { eid = "pdf-template-eid" });

// Deserialized to a type, e.g. one generated from the schema
var data = await anvil.RequestGraphQLAsync<Dictionary<string, Anvil.Types.GraphQL.Cast>>(query, variables);
var cast = data["cast"];
```

A mutation with an `Upload` argument, like `createCast`, takes a file from [`PrepareGraphQLFile`](#preparegraphqlfile) in its variables.

The response's `errors` throw an [`AnvilClientException`](#errors). `GetCurrentUserAsync()` is a shortcut for the `currentUser` query.

#### RequestRestAsync

Sends any REST request. The path is on the client's [`BaseUrl`](#baseurl), e.g. `/api/v1/generate-pdf`; a full URL is accepted if it is on the base URL, and refused otherwise.

```cs
// JSON in, JSON out
JToken json = await anvil.RequestRestAsync(HttpMethod.Post, "/api/v1/fill/pdf-template-eid.pdf", new
{
    data = new { name = "Sally" },
    outputFormat = "url",
});

// Deserialized to a type
var result = await anvil.RequestRestAsync<MyType>(HttpMethod.Get, "/api/v1/...");

// A binary response, as bytes or a stream
byte[] pdf = await anvil.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/generate-pdf", payload);
using Stream stream = await anvil.RequestRestStreamAsync(HttpMethod.Get, "/api/document-group/eid.zip");

// Query string parameters, and the checked HttpResponseMessage for anything else
var response = await anvil.SendRestAsync(HttpMethod.Post, "/api/v1/fill/eid.pdf",
    content: () => new StringContent(json, Encoding.UTF8, "application/json"),
    query: new Dictionary<string, string> { ["versionNumber"] = "-1" });
```

`RequestRestAsync` throws `InvalidOperationException` when the response isn't JSON; use the bytes or stream variant for a PDF or zip.

### Static methods

#### PrepareGraphQLFile

Prepares a file for a multipart upload with a GraphQL mutation, per the [GraphQL multipart request spec](https://github.com/jaydenseric/graphql-multipart-request-spec). The result goes anywhere in a mutation's variables under a `file` key: a `DocumentUpload` in [`CreateEtchPacketAsync`](#createetchpacketasync), or the `$file: Upload!` of a mutation sent with [`RequestGraphQLAsync`](#requestgraphqlasync).

```cs
// A file on disk; the filename defaults to the file's name
var upload = AnvilClient.PrepareGraphQLFile("/path/to/file.pdf");

// Bytes or a stream, with a filename. The mimetype is guessed from it when not given.
var upload = AnvilClient.PrepareGraphQLFile(bytes, "file.pdf");
var upload = AnvilClient.PrepareGraphQLFile(stream, "file.pdf", "application/pdf");

var data = await anvil.RequestGraphQLAsync(@"
    mutation CreateCast($title: String, $file: Upload!, $isTemplate: Boolean) {
      createCast(title: $title, file: $file, isTemplate: $isTemplate) { eid title }
    }",
    new { title = "My template", file = upload, isTemplate = true });
```

A request is sent again after a `429` (see [Rate limits](#rate-limits)): a path is reopened and a seekable stream rewound, while a stream that can't seek fails the retry with an error, so prefer a path or bytes. The caller's stream is not disposed.

For a small file, a `Base64Upload` (`Data`, `Filename`, `Mimetype`) is sent inline in the JSON instead.

### Options

`AnvilClientOptions`:

* `ApiKey` - your API key. See [Authentication](#authentication).
* `AccessToken` - an OAuth access token, instead of an API key.
* `BaseUrl` - the environment to use. Defaults to production. See [BaseUrl](#baseurl).
* `UserAgent` - the `User-Agent` header. Defaults to `dotnet-anvil/{version}`.
* `RequestLimit`, `RequestLimitMs` - the rate limit to start with, until the API's arrives. Defaults to 1 per 1000ms. See [Rate limits](#rate-limits).
* `Timeout` - how long to wait for a response, per request. Defaults to 5 minutes. See [Retries](#retries).
* `MaxRetries` - how many times to retry a `5xx` or a connection failure. Defaults to 5. See [Retries](#retries).
* `HttpClient` or `HttpMessageHandler` - to send requests through your own, e.g. one configured for a proxy, or an `IHttpClientFactory` client. The client does not dispose an `HttpClient` you pass in.

Every method also takes a `RequestOptions` with a `Timeout` for that request, and a `CancellationToken`.

#### Authentication

An API key, from your organization's API settings, is sent as `Authorization: Basic base64(apiKey + ":")`:

```cs
var anvil = new AnvilClient(new AnvilClientOptions { ApiKey = "..." });
```

When neither `ApiKey` nor `AccessToken` is set, the `ANVIL_API_KEY` environment variable is used.

An OAuth access token, from [OAuth](https://www.useanvil.com/docs/api/oauth), is sent as `Authorization: Bearer ...`: a JWT as-is, any other token base64 encoded. It takes precedence over an API key.

```cs
var anvil = new AnvilClient(new AnvilClientOptions { AccessToken = "..." });
```

#### BaseUrl

Every request derives from `BaseUrl`, which defaults to production, `https://app.useanvil.com`: REST requests go to `{BaseUrl}/api/v1/...`, GraphQL requests to `{BaseUrl}/graphql` and document downloads to `{BaseUrl}/api/document-group/...`. To use another environment:

```cs
var anvil = new AnvilClient(new AnvilClientOptions
{
    ApiKey = "...",
    BaseUrl = "https://<your-environment-url>",
});
```

Don't include a trailing slash. A full URL given to `RequestRestAsync` is accepted if it is on the base URL, and refused otherwise, so your credentials never go to another host.

### Constants

* `AnvilClient.VERSION_LATEST` (`-1`) - with `FillPdfAsync`, fills the latest version of the template, published or not. Use it to fill a draft.
* `AnvilClient.VERSION_LATEST_PUBLISHED` (`-2`) - fills the latest published version. This is the default when no version is given, except that it is an error when the template has never been published.

### Errors

An error response throws `AnvilClientException`: any status outside 2xx, a 2xx GraphQL response with `errors`, or a response that isn't the JSON it claims to be. A request that could not be made at all throws the underlying `HttpRequestException`, and one that got no response in time a `TimeoutException`.

```cs
try
{
    await anvil.FillPdfAsync("pdf-template-eid", new { });
}
catch (AnvilClientException e)
{
    // "400: ValidationError: Invalid request (data: Required)"
    Console.WriteLine(e.Message);
    Console.WriteLine(e.StatusCode);                 // 400; 0 when there was no response
    foreach (var error in e.Errors)                  // the normalized errors, at least one
    {
        Console.WriteLine($"{error.Name}: {error.Message}");
        foreach (var field in error.Fields)          // field-level details of a validation error
        {
            Console.WriteLine($"  {field.Property}: {field.Message}");
        }
    }
    e.Headers;          // the response's headers
    e.ResponseContent;  // the body, or the start of a large non-JSON body
    e.ResponseData;     // any `data` a GraphQL response returned alongside its errors
}
```

The errors are normalized from the API's shapes: a GraphQL response's `errors` as they are (each error's other members, like `path`, are in `Raw`), a REST error's `{ name, message, fields }` as one error, and a non-JSON error, like a `502` from a proxy, as one error named after the status text with the start of the body.

### Rate limits

The API [rate limits](https://www.useanvil.com/docs/api/basics#rate-limits) requests per API key, and tells the client the limit in the `X-RateLimit-Limit` and `X-RateLimit-Interval-Ms` headers of every response. The client learns it from the first response, holding any concurrent requests until that response arrives, then counts requests over a sliding window the way the API does and only waits when the limit has been reached, so the API never has to answer `429`. Every `AnvilClient` in the process with the same `BaseUrl` and credentials shares one limiter. `RateLimit` is the current limit and whether it came from the API.

Should the API answer `429` anyway (another process using the same API key at the same time can cause it), the request is retried after the response's `Retry-After` plus 50ms, or after a second when there is none, for as long as it takes. The `RateLimited` event is raised for each.

`RequestLimit` and `RequestLimitMs` are the limit to start with, used only until the first response. They default to 1 per second; set them to your account's limit to avoid that first second of pacing.

### Retries

Every request is retried on `500`, `502`, `503` and `504`, and when it couldn't connect, up to `MaxRetries` times (5) with exponential backoff from 100ms, or after the response's `Retry-After`. Set `MaxRetries` to 0 to turn it off.

A request that was sent but got no response within `Timeout` (5 minutes) throws `TimeoutException` and is not retried, since the API may have processed it. Pass a `RequestOptions { Timeout = ... }` to a method to override the timeout for that request.

## Earlier versions

The `RestClient` and `GraphQLClient` of versions before 1.0 still work, as thin wrappers over `AnvilClient`, and are marked obsolete; see [CHANGELOG.md](CHANGELOG.md) for what changed in 1.0 and how to move over.

## Examples

The [`examples/`](examples) project has runnable examples: filling a PDF, generating one from HTML or Markdown, and creating e-sign packets with a template, an embedded signer, and an uploaded PDF.

```bash
ANVIL_API_KEY=yourKey dotnet run --project examples -- fill
```

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md): building and testing, running every method against a real environment with `test-client/`, the generated types, and publishing.
