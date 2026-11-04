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
using System.Text;

using Anvil;
using Anvil.Client;
using Anvil.Payloads.Request.Types;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");
var baseUrl = Env("ANVIL_BASE_URL") ?? "https://staging.useanvil.com";
var accessToken = Env("ANVIL_ACCESS_TOKEN");
var pdfToSign = Path.Combine(AppContext.BaseDirectory, "assets", "dummy.pdf");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ANVIL_API_KEY is required. Usage: ANVIL_API_KEY=<your API key> dotnet run --project test-client");
    return 1;
}

Console.WriteLine($"Testing the Anvil .NET client against {baseUrl}\n");

using var rest = new RestClient(apiKey, baseUrl);
using var graphql = new GraphQLClient(apiKey, baseUrl);

var results = new List<(string Name, string Status)>();

// What the later steps need, found as we go
string? castEid = Env("ANVIL_PDF_TEMPLATE_ID");
JObject? castData = null;
string? forgeEid = Env("ANVIL_FORGE_EID");
string? etchPacketEid = null;
string? documentGroupEid = null;
string? signerEid = null;
string? weldDataEid = null;

await Check("SendQuery", async () =>
{
    var data = await graphql.SendQuery(@"
        query TestClient {
          currentUser { eid }
          organization {
            eid
            casts(limit: 50) { items { eid exampleData } }
            forges(isArchived: false) { eid }
          }
        }", null);
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
        var cast = await graphql.SendQuery(
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

await Check("rate limiting", () =>
    throw new Skip("Phase 2: the client has no rate limiter yet"));

await Check("OAuth access token", () =>
{
    if (accessToken == null)
    {
        throw new Skip("Set ANVIL_ACCESS_TOKEN to test Bearer auth");
    }

    throw new Skip("Phase 2: OAuth access tokens are not supported yet");
});

await Check("SendPostRequest", async () =>
{
    var body = JsonConvert.SerializeObject(new
    {
        title = "SendPostRequest",
        data = new[] { new { content = "Sent with SendPostRequest" } },
    });
    var response = await rest.SendPostRequest("generate-pdf", new StringContent(body, Encoding.UTF8, "application/json"));
    return AssertPdf(await response.Content.ReadAsByteArrayAsync());
});

await Check("GeneratePdf (markdown)", async () =>
{
    var payload = new Anvil.Payloads.Request.GeneratePdf
    {
        Title = "Markdown",
        Type = "markdown",
        Data = new List<IGeneratePdfListable>
        {
            new GeneratePdfItem { Label = "Hello", Content = "From the Anvil .NET client" },
            new GeneratePdfTable
            {
                Table = new GeneratePdfTableContent
                {
                    FirstRowHeaders = true,
                    Rows = new List<List<string>>
                    {
                        new() { "Method", "Status" },
                        new() { "GeneratePdf", "PASS" },
                    },
                },
            },
        },
    };
    var content = await rest.GeneratePdf(payload);
    return AssertPdf(await content.ReadAsByteArrayAsync());
});

await Check("GeneratePdf (HTML)", async () =>
{
    var payload = new Anvil.Payloads.Request.GeneratePdf
    {
        Title = "HTML",
        Type = "html",
        Data = new GeneratePdfHtml
        {
            Html = "<h1>Hello</h1><p>From the Anvil .NET client</p>",
            Css = "h1 { color: #4c5bf1; }",
        },
    };
    var content = await rest.GeneratePdf(payload);
    return AssertPdf(await content.ReadAsByteArrayAsync());
});

await Check("FillPdf", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    var stream = await rest.FillPdf(castEid!, FillPayload());
    return $"{castEid}, {AssertPdf(await ReadAll(stream))}";
});

await Check("FillPdf (VERSION_LATEST)", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    var stream = await rest.FillPdf(castEid!, FillPayload(), ClientConstants.VERSION_LATEST);
    return AssertPdf(await ReadAll(stream));
});

await Check("FillPdf (stream)", async () =>
{
    SkipUnless(castEid, "No PDF template with example data found. Set ANVIL_PDF_TEMPLATE_ID.");
    using var stream = await rest.FillPdf(castEid!, FillPayload());
    using var buffer = new MemoryStream();
    var chunk = new byte[64 * 1024];
    int read;
    while ((read = await stream.ReadAsync(chunk)) > 0)
    {
        buffer.Write(chunk, 0, read);
    }

    return AssertPdf(buffer.ToArray());
});

await Check("CreateEtchPacket (multipart upload)", () =>
    throw new Skip("Phase 4: multipart GraphQL file uploads are not supported yet (#26)"));

await Check("CreateEtchPacket (base64 upload)", async () =>
{
    // A base64 encoded file in the mutation's variables, as the client has always sent files
    var payload = new Anvil.Payloads.Request.CreateEtchPacket
    {
        Name = "Anvil .NET client test (base64 upload)",
        IsTest = true,
        // Send it right away, so it has signers that can sign
        IsDraft = false,
        Files = new IEtchPacketAttachable[]
        {
            new DocumentUpload
            {
                Id = "testFile",
                Title = "Test File",
                File = new Base64FileUpload
                {
                    Data = Convert.ToBase64String(File.ReadAllBytes(pdfToSign)),
                    Filename = "dummy.pdf",
                    Mimetype = "application/pdf",
                },
                Fields = new List<SignatureField>
                {
                    new()
                    {
                        Id = "signature",
                        Type = "signature",
                        PageNum = 0,
                        Rect = new Rect { X = 100, Y = 100, Width = 150, Height = 30 },
                    },
                },
            },
        },
        Signers = new[]
        {
            new EtchSigner
            {
                Id = "signer",
                Name = "Test Signer",
                Email = "test-signer@example.com",
                // Signs through a URL from GenerateEtchSignUrl rather than by email
                SignerType = "embedded",
                Fields = new[] { new SignerField { FileId = "testFile", FieldId = "signature" } },
            },
        },
    };
    var response = await graphql.CreateEtchPacket(payload);
    var created = (JObject?)response.CreateEtchPacket ?? throw new Exception("Expected createEtchPacket");
    etchPacketEid = created["eid"]?.ToString();
    documentGroupEid = created["documentGroup"]?["eid"]?.ToString();
    signerEid = (created["documentGroup"]?["signers"] as JArray)?.FirstOrDefault()?["eid"]?.ToString();
    return etchPacketEid ?? throw new Exception("Expected an eid");
});

await Check("CreateEtchPacket (in-person signer)", () =>
    throw new Skip("Phase 3: EtchSigner rejects the `in-person` signer type (#27)"));

await Check("SendQuery (createCast upload)", () =>
    throw new Skip("Phase 4: multipart GraphQL file uploads are not supported yet (#26)"));

await Check("GetEtchPacket", async () =>
{
    SkipUnless(etchPacketEid, "CreateEtchPacket failed");
    var response = await graphql.GetEtchPacket(etchPacketEid!);
    var packet = response.EtchPacket ?? throw new Exception("Expected etchPacket");
    if (packet.Eid != etchPacketEid)
    {
        throw new Exception("Got a different packet back");
    }

    return packet.DocumentGroup?.Status ?? "no document group";
});

await Check("GenerateEtchSignUrl", async () =>
{
    SkipUnless(signerEid, "CreateEtchPacket failed");
    var response = await graphql.GenerateEtchSignUrl(signerEid!, "dotnet-anvil-test-client");
    var url = response.GenerateEtchSignUrl;
    if (url == null || !(url.StartsWith("http://") || url.StartsWith("https://")))
    {
        throw new Exception($"Expected a URL, got `{url}`");
    }

    return "got a URL";
});

await Check("DownloadDocuments", async () =>
{
    SkipUnless(documentGroupEid, "CreateEtchPacket failed");
    var data = await ReadAll(await rest.DownloadDocuments(documentGroupEid!));
    // Zip files start with "PK"
    if (data.Length < 2 || data[0] != (byte)'P' || data[1] != (byte)'K')
    {
        throw new Exception("Expected a zip file");
    }

    return $"{data.Length} bytes";
});

await Check("ForgeSubmit", async () =>
{
    SkipUnless(forgeEid, "No webform (Forge) found in your organization. Set ANVIL_FORGE_EID.");
    var response = await graphql.ForgeSubmit(new Anvil.Payloads.Request.ForgeSubmit
    {
        ForgeEid = forgeEid,
        Payload = new { },
        IsTest = true,
    });
    var weldData = response.ForgeSubmit?.WeldData as JObject;
    weldDataEid = weldData?["eid"]?.ToString();
    return $"{forgeEid}, created {weldDataEid ?? throw new Exception("Expected weldData.eid")}";
});

await Check("RemoveWeldData", async () =>
{
    SkipUnless(weldDataEid, "ForgeSubmit did not create a submission");
    var response = await graphql.RemoveWeldData(weldDataEid!);
    if (response.RemoveWeldData != true)
    {
        throw new Exception("Expected true");
    }

    return $"removed {weldDataEid}";
});

await Check("GetCurrentUser", async () =>
{
    var response = await graphql.GetCurrentUser();
    return response.CurrentUser?["eid"]?.ToString() ?? throw new Exception("Expected currentUser.eid");
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
        var messages = e.Data.Keys.Cast<object>().Select(k => $"{k}: {e.Data[k]}");
        Console.WriteLine($"  FAIL  {name}: {e.Message}{(messages.Any() ? $" ({string.Join("; ", messages)})" : "")}");
    }
    catch (Exception e)
    {
        results.Add((name, "FAIL"));
        Console.WriteLine($"  FAIL  {name}: {e.GetType().Name}: {e.Message}");
    }
}

Anvil.Payloads.Request.FillPdf FillPayload() => new()
{
    Data = castData?.ToObject<Dictionary<string, dynamic>>() ?? new Dictionary<string, dynamic>(),
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

static async Task<byte[]> ReadAll(Stream stream)
{
    using (stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}

class Skip : Exception
{
    public Skip(string reason) : base(reason)
    {
    }
}
