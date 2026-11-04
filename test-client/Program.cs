// Runs every client method against a real Anvil environment (staging by
// default) and prints a pass or fail for each.
//
// Run it against a release before and after a change to see which behaviors
// changed. Everything it checks is the client's public behavior: what each
// method sends, and the shape of what it returns.
//
// Usage:
//   ANVIL_API_KEY=<your staging API key> dotnet run --project test-client
//
// Environment variables:
//   ANVIL_API_KEY          Required. An API key for the environment being tested.
//   ANVIL_BASE_URL         Optional. Defaults to https://staging.useanvil.com
//   ANVIL_ACCESS_TOKEN     Optional. An OAuth access token for the same environment,
//                          to test Bearer auth. Skipped when not set.
//   ANVIL_PDF_TEMPLATE_ID  Optional. The PDF template to fill. Defaults to the first
//                          one in your organization that has example data.
//   ANVIL_FORGE_EID        Optional. The webform (Forge) in a Workflow to submit to.
//                          Defaults to the first one in your organization.
//
// Everything this creates is test data: the Etch packets are test packets, and
// the Workflow submission is a test submission that gets removed at the end.
//
// A check that the client can't pass yet prints SKIP with the phase of the
// modernization plan that adds it, so the expected output of each phase is
// known in advance and a regression shows up as FAIL.
using System.Diagnostics;
using System.Net.Http;
using System.Text;

using Anvil;
using Anvil.Payloads;
using Anvil.Types.GraphQL;

using Newtonsoft.Json.Linq;

var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");
var baseUrl = Env("ANVIL_BASE_URL") ?? "https://staging.useanvil.com";
var accessToken = Env("ANVIL_ACCESS_TOKEN");
var pdfToSign = Path.Combine(AppContext.BaseDirectory, "assets", "dummy.pdf");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ANVIL_API_KEY is required. Usage: ANVIL_API_KEY=<your staging API key> dotnet run --project test-client");
    return 1;
}

Console.WriteLine($"Testing the Anvil .NET client {AnvilClient.Version} against {baseUrl}\n");

using var anvil = new AnvilClient(new AnvilClientOptions { ApiKey = apiKey, BaseUrl = baseUrl });

var results = new List<(string Name, string Status)>();

// What the later steps need, found as we go
string? castEid = Env("ANVIL_PDF_TEMPLATE_ID");
JObject? castData = null;
string? forgeEid = Env("ANVIL_FORGE_EID");
string? etchPacketEid = null;
string? documentGroupEid = null;
string? signerEid = null;
string? weldDataEid = null;

await Check("RequestGraphQLAsync", async () =>
{
    var data = await anvil.RequestGraphQLAsync(@"
        query TestClient {
          currentUser { eid }
          organization {
            eid
            casts(limit: 50) { items { eid exampleData } }
            forges(isArchived: false) { eid }
          }
        }");
    var currentUser = data["currentUser"] as JObject;
    var organization = data["organization"] as JObject;
    if (currentUser == null || organization == null)
    {
        throw new Exception("Expected currentUser and organization");
    }

    forgeEid ??= (organization["forges"] as JArray)?.FirstOrDefault()?["eid"]?.ToString();

    if (castEid != null)
    {
        // The API won't fill a PDF with empty data, so use the template's example data
        var cast = await anvil.RequestGraphQLAsync(
            "query TestClientCast ($eid: String!) { cast(eid: $eid) { exampleData } }",
            new { eid = castEid });
        castData = cast["cast"]?["exampleData"] as JObject;
    }
    else
    {
        var casts = organization["casts"]?["items"] as JArray ?? new JArray();
        var cast = casts.FirstOrDefault(c => c["exampleData"] is JObject);
        if (cast != null)
        {
            castEid = cast["eid"]?.ToString();
            castData = cast["exampleData"] as JObject;
        }
    }

    return $"organization {organization["eid"]}";
});

await Check("rate limiting", async () =>
{
    // The first response taught the client the account's limit. Send more
    // requests than it allows at once: the client must hold the extra ones
    // back for a full interval, and the API should never have to answer 429.
    var limit = anvil.RateLimit;
    if (!limit.LearnedFromResponse)
    {
        throw new Exception("The first response had no X-RateLimit-Limit and X-RateLimit-Interval-Ms headers");
    }

    var count = limit.Limit + 2;
    if (count > 50)
    {
        throw new Skip($"The limit is {limit.Limit} per {limit.IntervalMs}ms: too many to test in one burst");
    }

    var rateLimited = 0;
    void OnRateLimited(object? sender, RateLimitedEventArgs e) => Interlocked.Increment(ref rateLimited);
    anvil.RateLimited += OnRateLimited;
    var clock = Stopwatch.StartNew();
    try
    {
        await Task.WhenAll(Enumerable.Range(0, count).Select(_ => anvil.RequestGraphQLAsync("{ currentUser { eid } }")));
    }
    finally
    {
        anvil.RateLimited -= OnRateLimited;
    }

    var elapsed = clock.Elapsed;
    if (elapsed.TotalMilliseconds < limit.IntervalMs)
    {
        throw new Exception($"{count} requests went out in {elapsed.TotalSeconds:F2}s, faster than {limit.Limit} per {limit.IntervalMs}ms allows");
    }

    if (rateLimited > 0)
    {
        throw new Exception($"{rateLimited} of {count} requests got a 429 and were retried. The client paces requests so the API never has to answer 429; another process using this API key at the same time can cause it.");
    }

    return $"{limit.Limit} per {limit.IntervalMs}ms learned from the API; {count} requests took {elapsed.TotalSeconds:F2}s, no 429s";
});

await Check("OAuth access token", async () =>
{
    SkipUnless(accessToken, "Set ANVIL_ACCESS_TOKEN to test Bearer auth");
    using var oauth = new AnvilClient(new AnvilClientOptions { AccessToken = accessToken, BaseUrl = baseUrl });
    var data = await oauth.RequestGraphQLAsync("{ currentUser { eid } }");
    return $"user {data["currentUser"]?["eid"] ?? throw new Exception("Expected currentUser")}";
});

await Check("RequestRestBytesAsync", async () =>
{
    var pdf = await anvil.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/generate-pdf", new
    {
        title = "RequestRestBytesAsync",
        data = new[] { new { content = "Sent with RequestRestBytesAsync" } },
    });
    return AssertPdf(pdf);
});

await Check("GeneratePdfAsync (markdown)", async () =>
{
    var pdf = await anvil.GeneratePdfAsync(new GeneratePdfPayload
    {
        Title = "Markdown",
        Data = new List<MarkdownContent>
        {
            new() { Label = "Hello", Content = "From the Anvil .NET client" },
            new()
            {
                Table = new MarkdownTable
                {
                    FirstRowHeaders = true,
                    Rows = new List<List<string?>?>
                    {
                        new() { "Method", "Status" },
                        new() { "GeneratePdfAsync", "PASS" },
                    },
                },
            },
        },
    });
    return AssertPdf(pdf);
});

await Check("GeneratePdfAsync (HTML)", async () =>
{
    var pdf = await anvil.GeneratePdfAsync(new GeneratePdfPayload
    {
        Title = "HTML",
        Type = GeneratePdfPayload.TypeValues.Html,
        Data = new HtmlContent
        {
            Html = "<h1>Hello</h1><p>From the Anvil .NET client</p>",
            Css = "h1 { color: #4c5bf1; }",
        },
    });
    return AssertPdf(pdf);
});

await Check("FillPdfAsync", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    var pdf = await anvil.FillPdfAsync(castEid!, FillPayload());
    return $"{castEid}, {AssertPdf(pdf)}";
});

await Check("FillPdfAsync (VERSION_LATEST)", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    var pdf = await anvil.FillPdfAsync(castEid!, FillPayload(), AnvilClient.VERSION_LATEST);
    return AssertPdf(pdf);
});

await Check("FillPdfStreamAsync", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    using var stream = await anvil.FillPdfStreamAsync(castEid!, FillPayload());
    using var buffer = new MemoryStream();
    var chunk = new byte[64 * 1024];
    int read;
    while ((read = await stream.ReadAsync(chunk)) > 0)
    {
        buffer.Write(chunk, 0, read);
    }

    return AssertPdf(buffer.ToArray());
});

await Check("CreateEtchPacketAsync (multipart upload)", () =>
    throw new Skip("Phase 4: multipart GraphQL file uploads are not supported yet (#26)"));

await Check("CreateEtchPacketAsync (base64 upload)", async () =>
{
    // A base64 encoded file in the mutation's variables, as the client has always sent files
    var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
    {
        Name = "Anvil .NET client test (base64 upload)",
        IsTest = true,
        // Send it right away, so it has signers that can sign
        IsDraft = false,
        Files = new List<EtchFile>
        {
            new DocumentUpload
            {
                Id = "testFile",
                Title = "Test File",
                File = new Base64Upload
                {
                    Data = Convert.ToBase64String(System.IO.File.ReadAllBytes(pdfToSign)),
                    Filename = "dummy.pdf",
                },
                Fields = new List<object> { SignatureBox() },
            },
        },
        Signers = new List<object> { EmbeddedSigner() },
    });
    etchPacketEid = packet.Eid;
    documentGroupEid = packet.DocumentGroup?.Eid;
    signerEid = packet.DocumentGroup?.Signers?.FirstOrDefault()?.Eid;
    return etchPacketEid ?? throw new Exception("Expected an eid");
});

await Check("CreateEtchPacketAsync (in-person signer)", async () =>
{
    // A draft, so no one is asked to sign
    var packet = await anvil.CreateEtchPacketAsync(new CreateEtchPacketPayload
    {
        Name = "Anvil .NET client test (in-person signer)",
        IsTest = true,
        IsDraft = true,
        Files = new List<EtchFile>
        {
            new DocumentUpload
            {
                Id = "testFile",
                Title = "Test File",
                File = new Base64Upload { Data = Convert.ToBase64String(System.IO.File.ReadAllBytes(pdfToSign)), Filename = "dummy.pdf" },
                Fields = new List<object> { SignatureBox() },
            },
        },
        Signers = new List<object>
        {
            new EtchSigner
            {
                Id = "signer",
                Name = "Test Signer",
                Email = "test-signer@example.com",
                SignerType = EtchSigner.SignerTypeValues.InPerson,
                Fields = new List<object> { new SignerField { FileId = "testFile", FieldId = "signature" } },
            },
        },
    });
    return $"draft {packet.Eid}";
});

await Check("RequestGraphQLAsync (createCast upload)", () =>
    throw new Skip("Phase 4: multipart GraphQL file uploads are not supported yet (#26)"));

await Check("GetEtchPacketAsync", async () =>
{
    SkipUnless(etchPacketEid, "CreateEtchPacketAsync failed");
    var packet = await anvil.GetEtchPacketAsync(etchPacketEid!);
    if (packet.Eid != etchPacketEid)
    {
        throw new Exception("Got a different packet back");
    }

    return packet.Status ?? "no status";
});

await Check("GenerateEtchSignUrlAsync", async () =>
{
    SkipUnless(signerEid, "CreateEtchPacketAsync failed");
    var url = await anvil.GenerateEtchSignUrlAsync(signerEid!, "dotnet-anvil-test-client");
    if (!(url.StartsWith("http://") || url.StartsWith("https://")))
    {
        throw new Exception($"Expected a URL, got `{url}`");
    }

    return "got a URL";
});

await Check("DownloadDocumentsAsync", async () =>
{
    SkipUnless(documentGroupEid, "CreateEtchPacketAsync failed");
    var data = await anvil.DownloadDocumentsAsync(documentGroupEid!);
    // Zip files start with "PK"
    if (data.Length < 2 || data[0] != (byte)'P' || data[1] != (byte)'K')
    {
        throw new Exception("Expected a zip file");
    }

    return $"{data.Length} bytes";
});

await Check("ForgeSubmitAsync", async () =>
{
    SkipUnless(forgeEid, "No webform (Forge) found in your organization. Set ANVIL_FORGE_EID.");
    var submission = await anvil.ForgeSubmitAsync(new ForgeSubmitPayload
    {
        ForgeEid = forgeEid,
        Payload = new { },
        IsTest = true,
    });
    weldDataEid = submission.WeldData?.Eid;
    return $"{forgeEid}, created {weldDataEid ?? throw new Exception("Expected weldData.eid")}";
});

await Check("RemoveWeldDataAsync", async () =>
{
    SkipUnless(weldDataEid, "ForgeSubmitAsync did not create a submission");
    if (!await anvil.RemoveWeldDataAsync(weldDataEid!))
    {
        throw new Exception("Expected true");
    }

    return $"removed {weldDataEid}";
});

await Check("GetCurrentUserAsync", async () =>
{
    var user = await anvil.GetCurrentUserAsync();
    return user.Eid ?? throw new Exception("Expected currentUser.eid");
});

var passed = results.Count(r => r.Status == "PASS");
var failed = results.Count(r => r.Status == "FAIL");
var skipped = results.Count(r => r.Status == "SKIP");
Console.WriteLine($"\n{passed} passed, {failed} failed, {skipped} skipped");
return failed > 0 ? 1 : 0;

async Task Check(string name, Func<Task<string?>> fn)
{
    try
    {
        var detail = await fn();
        results.Add((name, "PASS"));
        Console.WriteLine($"  PASS  {name}{(detail != null ? $" ({detail})" : "")}");
    }
    catch (Skip e)
    {
        results.Add((name, "SKIP"));
        Console.WriteLine($"  SKIP  {name}: {e.Message}");
    }
    catch (AnvilClientException e)
    {
        results.Add((name, "FAIL"));
        Console.WriteLine($"  FAIL  {name}: {e.Message}");
    }
    catch (Exception e)
    {
        results.Add((name, "FAIL"));
        Console.WriteLine($"  FAIL  {name}: {e.GetType().Name}: {e.Message}");
    }
}

FillPdfPayload FillPayload() => new() { Data = castData ?? new JObject() };

static SignatureField SignatureBox() => new()
{
    Id = "signature",
    Type = SignatureField.TypeValues.Signature,
    PageNum = 0,
    Rect = new Rect { X = 100, Y = 100, Width = 150, Height = 30 },
};

static EtchSigner EmbeddedSigner() => new()
{
    Id = "signer",
    Name = "Test Signer",
    Email = "test-signer@example.com",
    // Signs through a URL from GenerateEtchSignUrlAsync rather than by email
    SignerType = EtchSigner.SignerTypeValues.Embedded,
    Fields = new List<object> { new SignerField { FileId = "testFile", FieldId = "signature" } },
};

static string? Env(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static void SkipUnless(object? value, string reason)
{
    if (value == null)
    {
        throw new Skip(reason);
    }
}

static string AssertPdf(byte[] data)
{
    if (data.Length < 5 || Encoding.ASCII.GetString(data, 0, 5) != "%PDF-")
    {
        throw new Exception("Expected PDF data");
    }

    return $"{data.Length} bytes";
}

class Skip : Exception
{
    public Skip(string reason) : base(reason)
    {
    }
}
