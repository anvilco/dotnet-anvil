namespace Anvil.Payloads.Request.Types
{
    public class EtchFile : IEtchPacketAttachable
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Filename { get; set; }
    }
}