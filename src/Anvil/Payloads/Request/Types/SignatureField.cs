namespace Anvil.Payloads.Request.Types
{
    public class SignatureField
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public int? PageNum { get; set; }
        public Rect? Rect { get; set; }
    }
}