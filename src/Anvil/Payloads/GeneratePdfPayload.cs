using System.Collections.Generic;

using Anvil.Types;
using Anvil.Types.Rest;

using Newtonsoft.Json;

namespace Anvil.Payloads
{
    /// <summary>
    /// The body of <see cref="AnvilClient.GeneratePdfAsync"/>. <c>Data</c> is a list
    /// of <see cref="MarkdownContent"/> for <c>Type = "markdown"</c> (the default),
    /// or an <see cref="HtmlContent"/> for <c>Type = "html"</c>. The union of the
    /// API's <see cref="GenerateMarkdownPdfRequest"/> and <see cref="GenerateHtmlPdfRequest"/>.
    /// </summary>
    public class GeneratePdfPayload : GeneratedModel
    {
        /// <summary>The values the API documents for <c>type</c>.</summary>
        public static class TypeValues
        {
            public const string Markdown = "markdown";
            public const string Html = "html";
        }

        /// <summary>The values the API documents for <c>outputFormat</c>.</summary>
        public static class OutputFormatValues
        {
            public const string Pdf = "pdf";
            public const string Url = "url";
        }

        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary><c>markdown</c> (the default) or <c>html</c>. See <see cref="TypeValues"/>.</summary>
        [JsonProperty("type")]
        public string? Type { get; set; }

        /// <summary>A list of <see cref="MarkdownContent"/>, or an <see cref="HtmlContent"/> when <c>Type</c> is <c>html</c>.</summary>
        [JsonProperty("data", Required = Required.Always)]
        public object? Data { get; set; }

        /// <summary>A <see cref="Logo"/>, or <c>false</c> for none.</summary>
        [JsonProperty("logo")]
        public object? Logo { get; set; }

        [JsonProperty("page")]
        public PageOptions? Page { get; set; }

        /// <summary>Between 5 and 30.</summary>
        [JsonProperty("fontSize")]
        public int? FontSize { get; set; }

        [JsonProperty("fontFamily")]
        public string? FontFamily { get; set; }

        [JsonProperty("textColor")]
        public string? TextColor { get; set; }

        [JsonProperty("includeTimestamp")]
        public bool? IncludeTimestamp { get; set; }

        /// <summary><c>pdf</c> (the default) or <c>url</c>. See <see cref="OutputFormatValues"/>.</summary>
        [JsonProperty("outputFormat")]
        public string? OutputFormat { get; set; }
    }

    /// <summary>One block of a Markdown PDF: a label and content, a heading, or a table.</summary>
    public class MarkdownContent : GenerateMarkdownPdfRequestData
    {
    }

    /// <summary>A table in a <see cref="MarkdownContent"/>.</summary>
    public class MarkdownTable : GenerateMarkdownPdfRequestDataTable
    {
    }

    /// <summary>The HTML and CSS of an HTML PDF, or of a <see cref="DocumentMarkup"/>.</summary>
    public class HtmlContent : GenerateHtmlPdfRequestData
    {
    }

    /// <summary>A logo for a generated PDF.</summary>
    public class Logo : GenerateMarkdownPdfRequestLogo
    {
    }
}
