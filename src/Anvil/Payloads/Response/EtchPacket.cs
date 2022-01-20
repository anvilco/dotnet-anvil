using Anvil.Payloads.Request.Types;

namespace Anvil.Payloads.Response
{
    public class EtchPacketPayload
    {
        public EtchPacketData? EtchPacket { get; set; }
    }

    public class SignerData
    {
        public int? Id { get; set; }
        public string? Eid { get; set; }
        public string? AliasId { get; set; }
        public int? RoutingOrder { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }
        public string? SignActionType { get; set; }
    }

    public class DocumentGroupData
    {
        public int? Id { get; set; }
        public string? Eid { get; set; }
        public string? Status { get; set; }
        public EtchFile[]? Files { get; set; }
        public SignerData[]? Signers { get; set; }
    }

    public class EtchPacketData
    {
        public int? Id { get; set; }
        public string? Eid { get; set; }
        public string? Name { get; set; }
        public string? DetailsURL { get; set; }
        public DocumentGroupData? DocumentGroup { get; set; }
    }
}