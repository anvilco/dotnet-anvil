using System.Text.Json.Serialization;

namespace Anvil.Payloads.Request.Types
{
    public class EtchCastRef : IEtchPacketAttachable
    {
        public string? Id { get; set; }
        
        [JsonPropertyName("castEid")] 
        public string? CastEid { get; set; }
    }
}