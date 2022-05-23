// Required to use `JSONProperty`. We don't want the built-in .NET version.
using Newtonsoft.Json;

using Anvil.Payloads.Request.Types;

namespace Anvil.Payloads.Request
{
    public class CreateEtchPacket
    {
        public string? Name { get; set; }
        public IEtchPacketAttachable[]? Files { get; set; }
        public bool? IsDraft { get; set; } = false;
        public bool? IsTest { get; set; } = true;
        public string? SignatureEmailSubject { get; set; }
        public string? SignatureEmailBody { get; set; }
        public object? SignaturePageOptions { get; set; }
        public string? ReplyToName { get; set; }
        public string? ReplyToEmail { get; set; }

        [JsonProperty("mergePDFs")]
        public bool? MergePdfs { get; set; }

        public EtchSigner[]? Signers { get; set; }
        public object? Data { get; set; }

        [JsonProperty("webhookURL")]
        public string? WebhookUrl { get; set; }

    }
}