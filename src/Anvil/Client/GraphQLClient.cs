using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Anvil.Payloads.Response;

using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Anvil.Client
{
    public class GraphQLClient : BaseClient
    {
        private readonly GraphQLHttpClient _graphQlHttpClient;

        public GraphQLClient(string apiKey)
        {
            _apiKey = apiKey;

            var httpOptions = new GraphQLHttpClientOptions {EndPoint = new Uri(Constants.GraphQLEndpoint)};

            var encodedKey = EncodeApiKey();
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {encodedKey}");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                NullValueHandling = NullValueHandling.Ignore
            };
            
            _graphQlHttpClient = new GraphQLHttpClient(httpOptions,
                // new SystemTextJsonSerializer(jsonSerializerOptions), httpClient);
                new NewtonsoftJsonSerializer(jsonSerializerSettings), httpClient);
            
            // If using the `System.Text.Json` serializer, we should use these options:
            //
            // var jsonSerializerOptions = new JsonSerializerOptions
            // {
            //     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //     PropertyNameCaseInsensitive = true,
            //     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // };
        }

        /// <summary>
        /// Sends an arbitrary GraphQL request to the Anvil API with the
        /// required authentication headers.
        ///
        /// Use this if you would like to send custom queries that aren't
        /// handled with any of the built-in queries.
        /// </summary>
        /// <param name="query">Full GraphQL query string</param>
        /// <param name="variables">Object containing variables for the query</param>
        /// <returns>JObject</returns>
        public async Task<JObject> SendQuery(string query, object? variables)
        {
            var request = new GraphQLHttpRequest()
            {
                Query = query,
                Variables = variables,
            };

            return await SendQuery<JObject>(request);
        }

        /// <summary>
        /// Sends GraphQL requests with the initialized GraphQL client.
        ///
        /// Use the overloaded `SendQuery(string s)` and `SendMutation(string s)` for
        /// queries and mutations without needing to create a full `GraphQLHttpRequest`
        /// object instance.
        /// </summary>
        /// <see cref="SendQuery" />
        /// 
        /// <param name="request">A GraphQLHttpRequest instance</param>
        /// <typeparam name="TResponse">Response type of the resulting GraphQL request</typeparam>
        private async Task<TResponse> SendQuery<TResponse>(GraphQLHttpRequest request)
        {
            try
            {
                var response = await _graphQlHttpClient.SendQueryAsync<TResponse>(request);
                if (response.Errors != null && response.Errors.Length > 0)
                {
                    var message = new StringBuilder();
                    foreach (var e in response.Errors)
                    {
                        message.Append(e.Message);
                    }
                    
                    throw new AnvilClientException(message.ToString());
                }

                return response.Data;
            }
            catch (GraphQLHttpRequestException ex)
            {
                if (ex.Content != null)
                {
                    var errorResponse = (JObject)JsonConvert.DeserializeObject(ex.Content);
                    var errors = (JArray)errorResponse["errors"];
                    var content = new StringBuilder();
                    foreach (var e in errors)
                    {
                        content.Append(e["message"]);
                    }

                    if (ex.Message.Contains("Unauthorized"))
                    {
                        throw new GraphQLHttpRequestException(ex.StatusCode, ex.ResponseHeaders, content.ToString());
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Sends an `etchPacket` query.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-etchpacket-Queries" />
        /// </summary>
        /// <param name="etchPacketEid"></param>
        /// <returns></returns>
        public async Task<Payloads.Response.EtchPacketPayload> GetEtchPacket(string etchPacketEid)
        {
            var query = new Queries.GetEtchPacket();
            var request = new GraphQLHttpRequest
            {
                Query = query.GetFullDefaultQuery(),
                Variables = new {Eid = etchPacketEid},
                OperationName = "GetEtchPacket"
            };

            return await SendQuery<Payloads.Response.EtchPacketPayload>(request);
        }

        /// <summary>
        /// Generates an Etch signing URL for use with "embedded" signer types.
        ///
        /// See API docs for more detail.
        /// <see href="https://www.useanvil.com/docs/api/e-signatures#controlling-the-signature-process-with-embedded-signers" />
        /// </summary>
        /// 
        /// <param name="signerEid"></param>
        /// <param name="clientUserId"></param>
        /// <returns></returns>
        public async Task<Payloads.Response.GenerateEtchSignUrlPayload> GenerateEtchSignUrl(
            string signerEid,
            string clientUserId
        )
        {
            var query = new Queries.GenerateEtchSignUrl();
            var request = new GraphQLHttpRequest
            {
                Query = query.GetFullDefaultQuery(),
                Variables = new {signerEid, clientUserId},
                OperationName = "GenerateEtchSignURL"
            };

            return await SendQuery<Payloads.Response.GenerateEtchSignUrlPayload>(request);
        }

        /// <summary>
        /// Handles `forgeSubmit` mutations.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-forgesubmit-Mutations"/>
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        public async Task<Payloads.Response.ForgeSubmitPayload> ForgeSubmit(Payloads.Request.ForgeSubmit requestPayload)
        {
            var query = new Queries.ForgeSubmit();
            var request = new GraphQLHttpRequest
            {
                Query = query.GetFullDefaultQuery(), Variables = requestPayload, OperationName = "ForgeSubmit"
            };

            return await SendQuery<Payloads.Response.ForgeSubmitPayload>(request);
        }

        public async Task<Payloads.Response.RemoveWeldDataPayload> RemoveWeldData(string weldEid)
        {
            var query = new Queries.RemoveWeldData();
            var request = new GraphQLHttpRequest
            {
                Query = query.GetFullDefaultQuery(),
                Variables = new Dictionary<string, string> {{"eid", weldEid}},
                OperationName = "RemoveWeldData"
            };

            return await SendQuery<Payloads.Response.RemoveWeldDataPayload>(request);
        }

        /// <summary>
        /// Sends a `currentUser` query.
        ///
        /// <see href="https://www.useanvil.com/docs/api/graphql/reference/#operation-currentuser-Queries" />
        /// </summary>
        /// <returns></returns>
        public async Task<Payloads.Response.CurrentUserPayload> GetCurrentUser()
        {
            var query = new Queries.CurrentUser();
            var request = new GraphQLHttpRequest {Query = query.GetFullDefaultQuery(),};

            return await SendQuery<Payloads.Response.CurrentUserPayload>(request);
        }

        public async Task<Payloads.Response.CreateEtchPacketPayload> CreateEtchPacket(Payloads.Request.CreateEtchPacket payload)
        {
            var query = new Queries.CreateEtchPacket();
            var request = new GraphQLHttpRequest
            {
                Query = query.GetFullDefaultQuery(),
                Variables = payload,
                OperationName = "CreateEtchPacket"
            };
            
            return await SendQuery<Payloads.Response.CreateEtchPacketPayload>(request);
        }
    }
}