namespace Anvil.Payloads.Request.Types
{
    public class GeneratePdfItem : IGeneratePdfListable
    {
        public string? Label { get; set; }
        public string? Content { get; set; }
        public string? FontSize { get; set; }
        public string? TextColor { get; set; }
    }
}