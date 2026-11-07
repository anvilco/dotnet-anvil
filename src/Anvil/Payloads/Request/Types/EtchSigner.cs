using System;

// Required to use `JSONProperty`. We don't want the built-in .NET version.
using Newtonsoft.Json;

namespace Anvil.Payloads.Request.Types
{
    public class EtchSigner
    {
        private string _signerType = "email";

        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public SignerField[]? Fields { get; set; }

        /// <summary><c>email</c> (the default), <c>embedded</c> or <c>in-person</c>. Any value the API accepts is sent as-is.</summary>
        public string SignerType
        {
            get => _signerType;
            set => _signerType = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int? RoutingOrder { get; set; }
        [JsonProperty("redirectURL")]
        public string? RedirectUrl { get; set; }
        public bool? AcceptEachField { get; set; }
        public string[]? EnableEmails { get; set; }
        public string? SignatureMode { get; set; }
    }
}
