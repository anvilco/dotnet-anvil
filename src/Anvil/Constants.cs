using System;

namespace Anvil
{
    static class Constants
    {
        // Production. Every URL the clients use is derived from the base URL
        // the client was constructed with, which defaults to this.
        public const string DefaultBaseUrl = "https://app.useanvil.com";

        public const int DefaultRequestLimit = 1;
        public const int DefaultRequestLimitMs = 1000;
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
        public const int DefaultMaxRetries = 5;

        // Added to every wait so a request never lands just inside the API's window
        public const int RateLimitBufferMs = 50;
        // What a 429 without a usable Retry-After waits, and the most any Retry-After is honored for
        public const int DefaultRetryAfterMs = 1000;
        public const int MaxRetryAfterMs = 60_000;
        // Backoff for 5xx and connection errors: this doubled per attempt
        public const int RetryBackoffBaseMs = 100;

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

        // Ends with a `/` so relative paths append to it. A path with a leading
        // `/` replaces the whole path instead (`/fill/my.pdf` would drop `api/v1`).
        public static string RestEndpoint(string? baseUrl) => NormalizeBaseUrl(baseUrl) + "/api/v1/";

        // https://www.useanvil.com/docs/api/fill-pdf
        public const string FillPdf = @"/api/v1/fill/{0}.pdf";

        // https://www.useanvil.com/docs/api/generate-pdf
        public const string GeneratePdf = "/api/v1/generate-pdf";

        // https://www.useanvil.com/docs/api/e-signatures#downloading-documents
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
