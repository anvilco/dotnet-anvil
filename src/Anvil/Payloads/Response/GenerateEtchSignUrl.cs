using System.Text.Json.Serialization;

namespace Anvil.Payloads.Response
{
    public class GenerateEtchSignUrlPayload
    {
        [JsonPropertyName("generateEtchSignURL")]
        public string? GenerateEtchSignUrl { get; set; }
    }
}