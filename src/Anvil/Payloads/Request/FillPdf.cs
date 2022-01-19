using System.Collections.Generic;

namespace Anvil.Payloads.Request
{
    public class FillPdf
    {
        public string? Title { set; get; }
        public int? FontSize { set; get; }
        public string? TextColor { set; get; }
        public Dictionary<string, dynamic>? Data { set; get; }
    }
}