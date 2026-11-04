// Runs one of the examples in this folder against your Anvil account:
//
//   ANVIL_API_KEY=yourKey dotnet run --project examples -- fill
//
// Set ANVIL_BASE_URL to use another environment.
using Anvil;

var examples = new Dictionary<string, Func<AnvilClient, string[], Task>>(StringComparer.OrdinalIgnoreCase)
{
    ["fill"] = AnvilExamples.Fill.Run,
    ["generate-html"] = AnvilExamples.GenerateHtml.Run,
    ["generate-markdown"] = AnvilExamples.GenerateMarkdown.Run,
    ["sign"] = AnvilExamples.Sign.Run,
    ["sign-embedded"] = AnvilExamples.SignEmbedded.Run,
    ["sign-upload"] = AnvilExamples.SignUpload.Run,
};

var apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");
if (args.Length == 0 || !examples.ContainsKey(args[0]) || string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Usage: ANVIL_API_KEY=yourKey dotnet run --project examples -- <example> [args]");
    Console.Error.WriteLine($"Examples: {string.Join(", ", examples.Keys)}");
    return 1;
}

using var anvil = new AnvilClient(new AnvilClientOptions
{
    ApiKey = apiKey,
    BaseUrl = Environment.GetEnvironmentVariable("ANVIL_BASE_URL") is { Length: > 0 } baseUrl ? baseUrl : "https://app.useanvil.com",
});

try
{
    await examples[args[0]](anvil, args.Skip(1).ToArray());
    return 0;
}
catch (AnvilClientException e)
{
    // The API's errors, normalized: status, name, message and any field details
    Console.Error.WriteLine($"The API returned an error: {e.Message}");
    return 1;
}
