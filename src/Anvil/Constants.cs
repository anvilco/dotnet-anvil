namespace Anvil
{
    static class Constants
    {
        public const string GraphQLEndpoint = "https://graphql.useanvil.com";

        // Remember to end the URI string here with a `/` since it will be appended onto
        // when the HTTP clients are used. *ALSO* when providing a URI path
        // (i.e. `RestClient.SendRequest("path/here", data);`), the path must _NOT_ 
        // have a starting `/` or the path will be rewritten.
        // For example: Using "/fill/my.pdf" will have a final URI of
        // "http://app.useanvil.com/fill/my.pdf", removing the "api/v1".
        public const string RestEndpoint = "https://app.useanvil.com/api/v1/";

        // https://www.useanvil.com/docs/api/fill-pdf
        public const string FillPdf = @"fill/{0}.pdf";

        // https://www.useanvil.com/docs/api/generate-pdf
        public const string GeneratePdf = "generate-pdf";

        // https://www.useanvil.com/docs/api/e-signatures#downloading-documents
        // In this case _we want_ the initial `/` because this does not use the base endpoint
        // above of `/api/v1/`.
        public const string DownloadDocuments = @"/api/document-group/{0}.zip";

        // Used with `RestClient.FillPdf`.
        // Version number to use for latest versions (usually drafts)
        public const int VERSION_LATEST = -1;
        // Version number to use for the latest published version.
        // This is the default when a version is not provided.
        public const int VERSION_LATEST_PUBLISHED = -2;
    }
}