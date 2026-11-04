using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Anvil.Client
{
    /// <summary>
    /// The pre-1.0 REST client, now a thin wrapper over <see cref="AnvilClient"/>,
    /// which rate limits, retries and reports errors the same way for REST and
    /// GraphQL. Use <see cref="AnvilClient"/> directly in new code.
    /// </summary>
    [Obsolete("Use AnvilClient, which has FillPdfAsync, GeneratePdfAsync and DownloadDocumentsAsync.")]
    public class RestClient : IDisposable
    {
        private readonly AnvilClient _anvil;
        private bool _disposed;

        public RestClient(string apiKey) : this(apiKey, null)
        {
        }

        /// <summary>
        /// Creates a REST client for the Anvil environment at <paramref name="baseUrl"/>.
        /// </summary>
        /// <param name="apiKey">Your API key</param>
        /// <param name="baseUrl">
        /// The environment's base URL, when Anvil has given you one. Defaults to
        /// production, https://app.useanvil.com. REST requests go to {baseUrl}/api/v1/
        /// and document downloads to {baseUrl}/api/document-group/.
        /// </param>
        public RestClient(string apiKey, string? baseUrl)
        {
            _anvil = new AnvilClient(apiKey, baseUrl);
        }

        internal RestClient(AnvilClient anvil)
        {
            _anvil = anvil;
        }

        /// <summary>The client the requests go through.</summary>
        public AnvilClient Anvil => _anvil;

        // Pre-1.0 paths were relative to /api/v1/, unless they started with a slash
        private static string Path(string uri) => uri.StartsWith("/") ? uri : "/api/v1/" + uri;

        /// <summary>GETs the URI (relative to <c>/api/v1/</c>) and returns the response, throwing <see cref="AnvilClientException"/> on an error status.</summary>
        public Task<HttpResponseMessage> SendGetRequest(string uri)
        {
            return _anvil.SendRestAsync(HttpMethod.Get, Path(uri));
        }

        /// <summary>POSTs to the URI (relative to <c>/api/v1/</c>) and returns the response, throwing <see cref="AnvilClientException"/> on an error status.</summary>
        public async Task<HttpResponseMessage> SendPostRequest(string uri, StringContent content)
        {
            // Re-created per attempt, since the request may be retried
            var body = await content.ReadAsStringAsync().ConfigureAwait(false);
            var mediaType = content.Headers.ContentType?.MediaType ?? "application/json";
            return await _anvil.SendRestAsync(HttpMethod.Post, Path(uri), () => new StringContent(body, Encoding.UTF8, mediaType)).ConfigureAwait(false);
        }

        public Task<Stream> FillPdf(string templateId, Payloads.Request.FillPdf payload)
        {
            return _anvil.RequestRestStreamAsync(HttpMethod.Post, string.Format(Constants.FillPdf, templateId), payload);
        }

        public async Task<bool> FillPdf(string templateId, Payloads.Request.FillPdf payload, string destFile)
        {
            using (var stream = await FillPdf(templateId, payload).ConfigureAwait(false))
            {
                await WriteFile(stream, destFile).ConfigureAwait(false);
            }

            return true;
        }

        public Task<Stream> FillPdf(string templateId, Payloads.Request.FillPdf payload, int versionNumber)
        {
            var query = new System.Collections.Generic.Dictionary<string, string> { ["versionNumber"] = versionNumber.ToString() };
            return _anvil.RequestRestStreamAsync(HttpMethod.Post, string.Format(Constants.FillPdf, templateId), payload, query);
        }

        public async Task<bool> FillPdf(string templateId, Payloads.Request.FillPdf payload, string destFile, int versionNumber)
        {
            using (var stream = await FillPdf(templateId, payload, versionNumber).ConfigureAwait(false))
            {
                await WriteFile(stream, destFile).ConfigureAwait(false);
            }

            return true;
        }

        public async Task<HttpContent> GeneratePdf(Payloads.Request.GeneratePdf payload)
        {
            var response = await _anvil.SendRestAsync(HttpMethod.Post, Constants.GeneratePdf, JsonContent(payload)).ConfigureAwait(false);
            return response.Content;
        }

        /// <summary>Sends a GeneratePDF request and saves the resulting data in the provided path.</summary>
        public async Task<bool> GeneratePdf(Payloads.Request.GeneratePdf payload, string destFile)
        {
            var content = await GeneratePdf(payload).ConfigureAwait(false);
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                await WriteFile(stream, destFile).ConfigureAwait(false);
            }

            return true;
        }

        public Task<Stream> DownloadDocuments(string documentGroupEid)
        {
            return _anvil.RequestRestStreamAsync(HttpMethod.Get, string.Format(Constants.DownloadDocuments, documentGroupEid));
        }

        private static Func<HttpContent?> JsonContent(object payload)
        {
            var body = Newtonsoft.Json.JsonConvert.SerializeObject(payload, AnvilClient.SerializerSettings);
            return () => AnvilClient.JsonContent(body);
        }

        private static async Task WriteFile(Stream stream, string destFile)
        {
            using (var output = new FileStream(destFile, FileMode.Create))
            {
                await stream.CopyToAsync(output).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _anvil.Dispose();
                _disposed = true;
            }
        }
    }
}
