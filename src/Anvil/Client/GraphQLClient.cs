using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Anvil.GraphQL;

using Newtonsoft.Json.Linq;

namespace Anvil.Client
{
    /// <summary>
    /// The pre-1.0 GraphQL client, now a thin wrapper over <see cref="AnvilClient"/>,
    /// which rate limits, retries and reports errors the same way for REST and
    /// GraphQL. Use <see cref="AnvilClient"/> directly in new code.
    /// </summary>
    [Obsolete("Use AnvilClient, which has CreateEtchPacketAsync, GetEtchPacketAsync, GenerateEtchSignUrlAsync, ForgeSubmitAsync, RemoveWeldDataAsync and RequestGraphQLAsync.")]
    public class GraphQLClient : IDisposable
    {
        private readonly AnvilClient _anvil;
        private bool _disposed;

        public GraphQLClient(string apiKey) : this(apiKey, null)
        {
        }

        /// <summary>
        /// Creates a GraphQL client for the Anvil environment at <paramref name="baseUrl"/>.
        /// </summary>
        /// <param name="apiKey">Your API key</param>
        /// <param name="baseUrl">
        /// The environment's base URL, when Anvil has given you one. Defaults to
        /// production, https://app.useanvil.com. GraphQL requests go to {baseUrl}/graphql.
        /// </param>
        public GraphQLClient(string apiKey, string? baseUrl)
        {
            _anvil = new AnvilClient(apiKey, baseUrl);
        }

        /// <summary>The client the requests go through.</summary>
        public AnvilClient Anvil => _anvil;

        /// <summary>
        /// Sends an arbitrary GraphQL request to the Anvil API with the
        /// required authentication headers.
        ///
        /// Use this if you would like to send custom queries that aren't
        /// handled with any of the built-in queries.
        /// </summary>
        /// <param name="query">Full GraphQL query string</param>
        /// <param name="variables">Object containing variables for the query</param>
        /// <returns>The response's <c>data</c></returns>
        public Task<JObject> SendQuery(string query, object? variables)
        {
            return _anvil.RequestGraphQLAsync(query, variables);
        }

        /// <summary>
        /// Sends an `etchPacket` query.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-etchpacket-Queries" />
        /// </summary>
        public Task<Payloads.Response.EtchPacketPayload> GetEtchPacket(string etchPacketEid)
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.EtchPacketPayload>(
                Operations.EtchPacketQuery(),
                new { Eid = etchPacketEid },
                "EtchPacket");
        }

        /// <summary>
        /// Generates an Etch signing URL for use with "embedded" signer types.
        ///
        /// <see href="https://www.useanvil.com/docs/api/e-signatures#controlling-the-signature-process-with-embedded-signers" />
        /// </summary>
        public Task<Payloads.Response.GenerateEtchSignUrlPayload> GenerateEtchSignUrl(string signerEid, string clientUserId)
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.GenerateEtchSignUrlPayload>(
                Operations.GenerateEtchSignUrlMutation(),
                new { signerEid, clientUserId },
                "GenerateEtchSignURL");
        }

        /// <summary>
        /// Handles `forgeSubmit` mutations.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-forgesubmit-Mutations"/>
        /// </summary>
        public Task<Payloads.Response.ForgeSubmitPayload> ForgeSubmit(Payloads.Request.ForgeSubmit requestPayload)
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.ForgeSubmitPayload>(
                Operations.ForgeSubmitMutation(),
                requestPayload,
                "ForgeSubmit");
        }

        public Task<Payloads.Response.RemoveWeldDataPayload> RemoveWeldData(string weldEid)
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.RemoveWeldDataPayload>(
                Operations.RemoveWeldDataMutation(),
                new Dictionary<string, string> { { "eid", weldEid } },
                "RemoveWeldData");
        }

        /// <summary>
        /// Sends a `currentUser` query.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-currentuser-Queries" />
        /// </summary>
        public Task<Payloads.Response.CurrentUserPayload> GetCurrentUser()
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.CurrentUserPayload>(Operations.CurrentUserQuery());
        }

        public Task<Payloads.Response.CreateEtchPacketPayload> CreateEtchPacket(Payloads.Request.CreateEtchPacket payload)
        {
            return _anvil.RequestGraphQLAsync<Payloads.Response.CreateEtchPacketPayload>(
                Operations.CreateEtchPacketMutation(),
                payload,
                "CreateEtchPacket");
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
