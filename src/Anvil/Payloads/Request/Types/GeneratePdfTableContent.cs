using System.Collections.Generic;

namespace Anvil.Payloads.Request.Types
{
    public class GeneratePdfTableContent
    {
        public List<List<string>>? Rows { get; set; }
        public List<object>? ColumnOptions { get; set; }
        public bool? FirstRowHeaders { get; set; }
        public bool? RowGridlines { get; set; }
        public bool? ColumnGridlines { get; set; }
        public string? VerticalAlign { get; set; }        
    }
}