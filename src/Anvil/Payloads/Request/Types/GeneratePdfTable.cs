namespace Anvil.Payloads.Request.Types
{
    public class GeneratePdfTable : IGeneratePdfListable
    {
        public GeneratePdfTableContent? Table { get; set; }
    }
}