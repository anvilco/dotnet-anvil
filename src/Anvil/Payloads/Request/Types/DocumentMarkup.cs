using System.Collections.Generic;

namespace Anvil.Payloads.Request.Types
{
    public class HtmlCssMarkup
    {
        public string Html { get; set; }
        public string? Css { get; set; }
    }

    public class DocumentMarkup : IEtchPacketAttachable
    {
        public string? Id { get; set; }
        public string? Filename { get; set; }
        public HtmlCssMarkup Markup { get; set; }
        public List<SignatureField>? Fields { get; set; }
        public string? Title { get; set; }
        public int? FontSize { get; set; }
        public string? TextColor { get; set; }
    }
}