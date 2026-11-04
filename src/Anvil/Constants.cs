using System;

namespace Anvil
{
    static class Constants
    {
        // Production. Every URL the clients use is derived from the base URL
        // the client was constructed with, which defaults to this.
        public const string DefaultBaseUrl = "https://app.useanvil.com";

        public static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return DefaultBaseUrl;
            }

            var trimmed = baseUrl!.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(uri.Host))
            {
                throw new ArgumentException($"baseUrl must be an http or https URL, got `{baseUrl}`", nameof(baseUrl));
            }

            return trimmed;
        }

        public static string GraphQLEndpoint(string? baseUrl) => NormalizeBaseUrl(baseUrl) + "/graphql";

        // Remember to end the URI string here with a `/` since it will be appended onto
        // when the HTTP clients are used. *ALSO* when providing a URI path
        // (i.e. `RestClient.SendRequest("path/here", data);`), the path must _NOT_
        // have a starting `/` or the path will be rewritten.
        // For example: Using "/fill/my.pdf" will have a final URI of
        // "http://app.useanvil.com/fill/my.pdf", removing the "api/v1".
        public static string RestEndpoint(string? baseUrl) => NormalizeBaseUrl(baseUrl) + "/api/v1/";

        // https://www.useanvil.com/docs/api/fill-pdf
        public const string FillPdf = @"fill/{0}.pdf";

        // https://www.useanvil.com/docs/api/generate-pdf
        public const string GeneratePdf = "generate-pdf";

        // https://www.useanvil.com/docs/api/e-signatures#downloading-documents
        // In this case _we want_ the initial `/` because this does not use the base endpoint
        // above of `/api/v1/`.
        public const string DownloadDocuments = @"/api/document-group/{0}.zip";
    }

    public static class ClientConstants
    {
        // Used with `RestClient.FillPdf`.
        // Version number to use for latest versions (usually drafts)
        public const int VERSION_LATEST = -1;

        // Version number to use for the latest published version.
        // This is the default when a version is not provided.
        public const int VERSION_LATEST_PUBLISHED = -2;
    }
}
