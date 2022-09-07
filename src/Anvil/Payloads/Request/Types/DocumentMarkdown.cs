using System.Collections.Generic;

namespace Anvil.Payloads.Request.Types
{
    public class DocumentMarkdown : IEtchPacketAttachable
    {
        public string? Id { get; set; }
        public string? Filename { get; set; }
        public List<IGeneratePdfListable>? Fields { get; set; }
        public string? Title { get; set; }
        public int? FontSize { get; set; }
        public string? TextColor { get; set; }
    }
}