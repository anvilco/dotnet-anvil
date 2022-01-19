using System.Collections.Generic;

namespace Anvil.Payloads.Request.Types
{
    public class DocumentUpload : IEtchPacketAttachable
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        // A GraphQLUpload or Base64Upload, but using Bas64Upload for now
        public Base64FileUpload? File { get; set; } 
        public List<SignatureField>? Fields { get; set; } 
        public int? FontSize { get; set; } 
        public string? TextColor { get; set; } 
    }
}