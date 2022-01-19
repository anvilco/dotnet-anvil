using System.Text.Json.Serialization;

using Newtonsoft.Json.Linq;

namespace Anvil.Payloads.Response
{
    public class CreateEtchPacketPayload
    {
        public dynamic? CreateEtchPacket;
    }
    public class CreateEtchPacketData
    {
        public string? Id { get; set; }

        public string? Eid { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("detailsURL")]
        public string? DetailsUrl { get; set; }

        public JObject? DocumentGroup { get; set; }
    }
}