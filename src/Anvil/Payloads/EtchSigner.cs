using Anvil.Types;
using Anvil.Types.Rest;

using Newtonsoft.Json;

namespace Anvil.Payloads
{
    /// <summary>
    /// A signer of an Etch packet: who signs, which fields they sign
    /// (<see cref="SignerField"/>s in <c>Fields</c>) and how. Every option the API
    /// documents, generated from its OpenAPI spec in <see cref="Types.Rest.EtchSigner"/>.
    /// <c>SignerType</c> is <c>email</c> by default; <c>embedded</c> signs through a
    /// URL from <see cref="AnvilClient.GenerateEtchSignUrlAsync"/>, and
    /// <c>in-person</c> on the sender's device. See <see cref="Types.Rest.EtchSigner.SignerTypeValues"/>.
    /// </summary>
    public class EtchSigner : Types.Rest.EtchSigner
    {
        public EtchSigner()
        {
            SignerType = SignerTypeValues.Email;
        }
    }

    /// <summary>A field a signer signs: the field's id on a file in the packet.</summary>
    public class SignerField : EtchSignerField
    {
    }

    /// <summary>A field placed on an uploaded or generated document, e.g. a signature box.</summary>
    public class SignatureField : GeneratedModel
    {
        /// <summary>The values the API documents for <c>type</c>.</summary>
        public static class TypeValues
        {
            public const string Signature = "signature";
            public const string Initial = "initial";
            public const string SignatureDate = "signatureDate";
            public const string SignerName = "signerName";
            public const string SignerEmail = "signerEmail";
        }

        [JsonProperty("id", Required = Required.Always)]
        public string? Id { get; set; }

        /// <summary>See <see cref="TypeValues"/>.</summary>
        [JsonProperty("type", Required = Required.Always)]
        public string? Type { get; set; }

        /// <summary>The page the field is on, starting at 0.</summary>
        [JsonProperty("pageNum", Required = Required.Always)]
        public int? PageNum { get; set; }

        [JsonProperty("rect", Required = Required.Always)]
        public Rect? Rect { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    /// <summary>Where a <see cref="SignatureField"/> sits on its page, in points.</summary>
    public class Rect : GeneratedModel
    {
        [JsonProperty("x")]
        public double? X { get; set; }

        [JsonProperty("y")]
        public double? Y { get; set; }

        [JsonProperty("width")]
        public double? Width { get; set; }

        [JsonProperty("height")]
        public double? Height { get; set; }
    }
}
