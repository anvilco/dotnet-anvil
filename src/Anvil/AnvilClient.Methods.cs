using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Anvil.GraphQL;
using Anvil.Types.GraphQL;

using Newtonsoft.Json.Linq;

namespace Anvil
{
    // The API methods. Each takes its payload as any object that serializes to
    // the JSON the API expects: the payload types in Anvil.Payloads, an anonymous
    // object, a dictionary, a JObject or a JSON string.
    public partial class AnvilClient
    {
        // ------------------------------------------------------------------
        // PDF filling and generation

        /// <summary>
        /// Fills a PDF template with data and returns the PDF.
        /// <see href="https://www.useanvil.com/docs/api/fill-pdf"/>
        /// </summary>
        /// <param name="pdfTemplateId">The template's eid, from its page in the dashboard</param>
        /// <param name="payload">A <see cref="Payloads.FillPdfPayload"/>, or anything with the same JSON shape</param>
        /// <param name="versionNumber">
        /// A version of the template to fill, <see cref="VERSION_LATEST"/> for the latest
        /// draft, or <see cref="VERSION_LATEST_PUBLISHED"/>. When left out, the latest
        /// published version is used, or the latest draft if never published.
        /// </param>
        /// <returns>The PDF, or the JSON response's bytes when the payload asks for <c>outputFormat: "url"</c></returns>
        public Task<byte[]> FillPdfAsync(string pdfTemplateId, object payload, int? versionNumber = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestBytesAsync(HttpMethod.Post, FillPdfPath(pdfTemplateId), Payload(payload), VersionQuery(versionNumber), options, cancellationToken);
        }

        /// <summary>Like <see cref="FillPdfAsync"/>, returning the PDF as a stream to read without holding it in memory. Dispose it when done.</summary>
        public Task<Stream> FillPdfStreamAsync(string pdfTemplateId, object payload, int? versionNumber = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestStreamAsync(HttpMethod.Post, FillPdfPath(pdfTemplateId), Payload(payload), VersionQuery(versionNumber), options, cancellationToken);
        }

        /// <summary>Like <see cref="FillPdfAsync"/>, writing the PDF to <paramref name="path"/>.</summary>
        public async Task FillPdfToFileAsync(string pdfTemplateId, object payload, string path, int? versionNumber = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            using (var stream = await FillPdfStreamAsync(pdfTemplateId, payload, versionNumber, options, cancellationToken).ConfigureAwait(false))
            {
                await WriteFileAsync(stream, path, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Generates a PDF from Markdown or HTML and returns it.
        /// <see href="https://www.useanvil.com/docs/api/generate-pdf"/>
        /// </summary>
        /// <param name="payload">A <see cref="Payloads.GeneratePdfPayload"/>, or anything with the same JSON shape</param>
        public Task<byte[]> GeneratePdfAsync(object payload, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestBytesAsync(HttpMethod.Post, Constants.GeneratePdf, Payload(payload), null, options, cancellationToken);
        }

        /// <summary>Like <see cref="GeneratePdfAsync"/>, returning the PDF as a stream. Dispose it when done.</summary>
        public Task<Stream> GeneratePdfStreamAsync(object payload, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestStreamAsync(HttpMethod.Post, Constants.GeneratePdf, Payload(payload), null, options, cancellationToken);
        }

        /// <summary>Like <see cref="GeneratePdfAsync"/>, writing the PDF to <paramref name="path"/>.</summary>
        public async Task GeneratePdfToFileAsync(object payload, string path, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            using (var stream = await GeneratePdfStreamAsync(payload, options, cancellationToken).ConfigureAwait(false))
            {
                await WriteFileAsync(stream, path, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Downloads the documents of an Etch packet's document group as a zip.
        /// <see href="https://www.useanvil.com/docs/api/e-signatures#downloading-documents"/>
        /// </summary>
        public Task<byte[]> DownloadDocumentsAsync(string documentGroupEid, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestBytesAsync(HttpMethod.Get, DownloadDocumentsPath(documentGroupEid), null, null, options, cancellationToken);
        }

        /// <summary>Like <see cref="DownloadDocumentsAsync"/>, returning the zip as a stream. Dispose it when done.</summary>
        public Task<Stream> DownloadDocumentsStreamAsync(string documentGroupEid, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return RequestRestStreamAsync(HttpMethod.Get, DownloadDocumentsPath(documentGroupEid), null, null, options, cancellationToken);
        }

        /// <summary>Like <see cref="DownloadDocumentsAsync"/>, writing the zip to <paramref name="path"/>.</summary>
        public async Task DownloadDocumentsToFileAsync(string documentGroupEid, string path, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            using (var stream = await DownloadDocumentsStreamAsync(documentGroupEid, options, cancellationToken).ConfigureAwait(false))
            {
                await WriteFileAsync(stream, path, cancellationToken).ConfigureAwait(false);
            }
        }

        // ------------------------------------------------------------------
        // Etch e-signatures

        /// <summary>
        /// Creates an Etch packet and, unless it is a draft, sends it to its first signer.
        /// <see href="https://www.useanvil.com/docs/api/e-signatures"/>
        /// </summary>
        /// <param name="payload">A <see cref="Payloads.CreateEtchPacketPayload"/>, or anything with the same JSON shape: the mutation's variables</param>
        /// <param name="responseQuery">The fields to return, instead of <see cref="Operations.CreateEtchPacketDefaultResponseQuery"/></param>
        /// <param name="mutation">A whole mutation document to send instead of <see cref="Operations.CreateEtchPacketMutation"/></param>
        /// <returns>The packet, with the fields the response query selected; others are in <c>AdditionalData</c></returns>
        public async Task<EtchPacket> CreateEtchPacketAsync(object payload, string? responseQuery = null, string? mutation = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(mutation ?? Operations.CreateEtchPacketMutation(responseQuery), Payload(payload), mutation == null ? "CreateEtchPacket" : null, options, cancellationToken).ConfigureAwait(false);
            return Field<EtchPacket>(data, "createEtchPacket");
        }

        /// <summary>Gets an Etch packet: its status, documents and signers.</summary>
        /// <param name="responseQuery">The fields to return, instead of <see cref="Operations.EtchPacketDefaultResponseQuery"/></param>
        public async Task<EtchPacket> GetEtchPacketAsync(string etchPacketEid, string? responseQuery = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(Operations.EtchPacketQuery(responseQuery), new { eid = etchPacketEid }, "EtchPacket", options, cancellationToken).ConfigureAwait(false);
            return Field<EtchPacket>(data, "etchPacket");
        }

        /// <summary>
        /// Generates the URL an <c>embedded</c> signer signs at.
        /// <see href="https://www.useanvil.com/docs/api/e-signatures#controlling-the-signature-process-with-embedded-signers"/>
        /// </summary>
        /// <param name="signerEid">The signer's eid, from the packet's <c>documentGroup.signers</c></param>
        /// <param name="clientUserId">The signer's id in your system</param>
        public async Task<string> GenerateEtchSignUrlAsync(string signerEid, string clientUserId, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(Operations.GenerateEtchSignUrlMutation(), new { signerEid, clientUserId }, "GenerateEtchSignURL", options, cancellationToken).ConfigureAwait(false);
            return Field<string>(data, "generateEtchSignURL");
        }

        // ------------------------------------------------------------------
        // Workflows

        /// <summary>
        /// Submits data to a Workflow's webform (<c>Forge</c>), creating or updating a submission.
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-forgesubmit-Mutations"/>
        /// </summary>
        /// <param name="payload">A <see cref="Payloads.ForgeSubmitPayload"/>, or anything with the same JSON shape: the mutation's variables</param>
        /// <param name="responseQuery">The fields to return, instead of <see cref="Operations.ForgeSubmitDefaultResponseQuery"/></param>
        /// <param name="mutation">A whole mutation document to send instead of <see cref="Operations.ForgeSubmitMutation"/></param>
        public async Task<Submission> ForgeSubmitAsync(object payload, string? responseQuery = null, string? mutation = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(mutation ?? Operations.ForgeSubmitMutation(responseQuery), Payload(payload), mutation == null ? "ForgeSubmit" : null, options, cancellationToken).ConfigureAwait(false);
            return Field<Submission>(data, "forgeSubmit");
        }

        /// <summary>Removes a Workflow submission's data (<c>WeldData</c>). Returns whether it was removed.</summary>
        public async Task<bool> RemoveWeldDataAsync(string weldDataEid, string? mutation = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(mutation ?? Operations.RemoveWeldDataMutation(), new { eid = weldDataEid }, mutation == null ? "RemoveWeldData" : null, options, cancellationToken).ConfigureAwait(false);
            return Field<bool>(data, "removeWeldData");
        }

        // ------------------------------------------------------------------
        // Other queries

        /// <summary>The user the API key or access token belongs to.</summary>
        /// <param name="responseQuery">The fields to return, instead of <see cref="Operations.CurrentUserDefaultResponseQuery"/></param>
        public async Task<User> GetCurrentUserAsync(string? responseQuery = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(Operations.CurrentUserQuery(responseQuery), null, "CurrentUser", options, cancellationToken).ConfigureAwait(false);
            return Field<User>(data, "currentUser");
        }

        // ------------------------------------------------------------------

        private static string FillPdfPath(string pdfTemplateId)
        {
            if (string.IsNullOrWhiteSpace(pdfTemplateId))
            {
                throw new ArgumentException("pdfTemplateId is required", nameof(pdfTemplateId));
            }

            return string.Format(Constants.FillPdf, Uri.EscapeDataString(pdfTemplateId));
        }

        private static string DownloadDocumentsPath(string documentGroupEid)
        {
            if (string.IsNullOrWhiteSpace(documentGroupEid))
            {
                throw new ArgumentException("documentGroupEid is required", nameof(documentGroupEid));
            }

            return string.Format(Constants.DownloadDocuments, Uri.EscapeDataString(documentGroupEid));
        }

        private static IDictionary<string, string>? VersionQuery(int? versionNumber)
        {
            return versionNumber.HasValue
                ? new Dictionary<string, string> { ["versionNumber"] = versionNumber.Value.ToString(CultureInfo.InvariantCulture) }
                : null;
        }

        /// <summary>A payload given as a JSON string is parsed; anything else is serialized as it is.</summary>
        private static object Payload(object payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return payload is string json ? JToken.Parse(json) : payload;
        }

        private static T Field<T>(JObject data, string name)
        {
            var token = data[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                throw new AnvilClientException($"The response has no `{name}`: {data.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            return token.ToObject<T>(Serializer)!;
        }

        private static async Task WriteFileAsync(Stream stream, string path, CancellationToken cancellationToken)
        {
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await stream.CopyToAsync(file, 81920, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
