using System;
using System.Text.Json.Serialization;

namespace Anvil.Payloads.Request.Types
{
    public class EtchSigner
    {
        private string _signerType = "email";
        
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public SignerField[]? Fields { get; set; }

        public string SignerType
        {
            get => _signerType;
            set
            {
                if (value.ToLower() == "email" || value.ToLower() == "embedded")
                {
                    _signerType = value;
                }
                else
                {
                    throw new ArgumentException("SignerType must be either `email` or `embedded`");
                }
            }
        }

        public int? RoutingOrder { get; set; }
        [JsonPropertyName("redirectURL")]
        public string? RedirectUrl { get; set; }
        public bool? AcceptEachField { get; set; }
        public string[]? EnableEmails { get; set; }
        public string? SignatureMode { get; set; }
    }
}