using Newtonsoft.Json;

namespace Anvil.Payloads.Response
{
    public class GenerateEtchSignUrlPayload
    {
        [JsonProperty("generateEtchSignURL")]
        public string? GenerateEtchSignUrl { get; set; }
    }
}
