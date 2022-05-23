// Required to use `JSONProperty`. We don't want the built-in .NET version.
using Newtonsoft.Json;

namespace Anvil.Payloads.Request.Types
{
    public class EtchCastRef : IEtchPacketAttachable
    {
        public string? Id { get; set; }

        [JsonProperty("castEid")]
        public string? CastEid { get; set; }
    }
}