using Anvil.Types;
using Anvil.Types.GraphQL;

using Newtonsoft.Json;

namespace Anvil.Payloads
{
    /// <summary>
    /// A file in an Etch packet that references one of your PDF templates
    /// (<c>Casts</c>) by its eid. <c>Id</c> is the name signers' fields refer to it by.
    /// </summary>
    public class EtchCastRef : EtchFile
    {
    }

    /// <summary>
    /// A PDF uploaded with the packet. <c>File</c> is a <see cref="Base64Upload"/>,
    /// a <see cref="UrlUpload"/>, or a file prepared with
    /// <see cref="AnvilClient.PrepareGraphQLFile(string, string?, string?)"/> for a
    /// multipart upload. <c>Fields</c> are the <see cref="SignatureField"/>s to place on it.
    /// </summary>
    public class DocumentUpload : EtchFile
    {
    }

    /// <summary>
    /// A PDF generated from HTML and CSS (<c>Markup</c>, an <see cref="HtmlContent"/>)
    /// when the packet is created, with <see cref="SignatureField"/>s in <c>Fields</c>.
    /// </summary>
    public class DocumentMarkup : EtchFile
    {
    }

    /// <summary>
    /// A PDF generated from Markdown when the packet is created: <c>Fields</c> holds
    /// <see cref="MarkdownContent"/> blocks and <see cref="SignatureField"/>s.
    /// </summary>
    public class DocumentMarkdown : EtchFile
    {
    }

    /// <summary>A file sent inline in the JSON, base64 encoded. For large files, prefer <see cref="AnvilClient.PrepareGraphQLFile(string, string?, string?)"/>.</summary>
    public class Base64Upload : GeneratedModel
    {
        [JsonProperty("data", Required = Required.Always)]
        public string? Data { get; set; }

        [JsonProperty("filename", Required = Required.Always)]
        public string? Filename { get; set; }

        [JsonProperty("mimetype")]
        public string? Mimetype { get; set; } = "application/pdf";
    }

    /// <summary>
    /// A file Anvil downloads from a public <c>http</c> or <c>https</c> URL. Without a
    /// <c>Filename</c>, Anvil takes it from the response's <c>Content-Disposition</c> or the URL.
    /// </summary>
    public class UrlUpload : GeneratedModel
    {
        [JsonProperty("url", Required = Required.Always)]
        public string? Url { get; set; }

        [JsonProperty("filename")]
        public string? Filename { get; set; }
    }
}
