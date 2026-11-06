using System;
using System.Net.Http;

namespace Anvil
{
    /// <summary>
    /// How an <see cref="AnvilClient"/> connects to Anvil. Either <see cref="ApiKey"/>
    /// or <see cref="AccessToken"/> is required.
    /// </summary>
    public class AnvilClientOptions
    {
        /// <summary>
        /// Your API key, from the organization's API settings. Sent as
        /// <c>Authorization: Basic base64(apiKey + ":")</c>. Falls back to the
        /// <c>ANVIL_API_KEY</c> environment variable when neither this nor
        /// <see cref="AccessToken"/> is set.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// An OAuth access token, sent as <c>Authorization: Bearer ...</c>. Takes
        /// precedence over <see cref="ApiKey"/> when both are set. A JWT is sent
        /// as-is; any other token is base64 encoded, like node-anvil does.
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// The environment's base URL, when Anvil has given you one for another environment.
        /// Defaults to production, <c>https://app.useanvil.com</c>. Every request
        /// derives from it: REST requests go to <c>{BaseUrl}/api/v1/...</c>, GraphQL
        /// requests to <c>{BaseUrl}/graphql</c> and document downloads to
        /// <c>{BaseUrl}/api/document-group/...</c>.
        /// </summary>
        public string BaseUrl { get; set; } = Constants.DefaultBaseUrl;

        /// <summary>The <c>User-Agent</c> header. Defaults to <c>dotnet-anvil/{version}</c>.</summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// The number of requests per <see cref="RequestLimitMs"/> to start with,
        /// until the API's own limit arrives in the first response's
        /// <c>X-RateLimit-Limit</c> and <c>X-RateLimit-Interval-Ms</c> headers.
        /// Defaults to 1.
        /// </summary>
        public int RequestLimit { get; set; } = Constants.DefaultRequestLimit;

        /// <summary>The interval, in milliseconds, of <see cref="RequestLimit"/>. Defaults to 1000.</summary>
        public int RequestLimitMs { get; set; } = Constants.DefaultRequestLimitMs;

        /// <summary>
        /// How long to wait for a response before giving up, per request. Defaults to
        /// 5 minutes. A request that times out throws <see cref="TimeoutException"/>
        /// and is not retried, since the API may have processed it.
        /// </summary>
        public TimeSpan Timeout { get; set; } = Constants.DefaultTimeout;

        /// <summary>
        /// How many times to retry a request that got a <c>500</c>, <c>502</c>,
        /// <c>503</c> or <c>504</c>, or couldn't connect, with exponential backoff
        /// from 100ms. Defaults to 5; 0 turns it off. Rate limited (<c>429</c>)
        /// requests are always retried after their <c>Retry-After</c>.
        /// </summary>
        public int MaxRetries { get; set; } = Constants.DefaultMaxRetries;

        /// <summary>
        /// An <see cref="System.Net.Http.HttpClient"/> to send requests with, e.g. one
        /// configured for a proxy, or an <c>IHttpClientFactory</c> client. The
        /// <see cref="AnvilClient"/> does not dispose it. When not set, one is created
        /// from <see cref="HttpMessageHandler"/> or the default handler.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// The handler to create the <see cref="System.Net.Http.HttpClient"/> with when
        /// <see cref="HttpClient"/> is not set. Ignored when it is. Useful for tests
        /// and proxies.
        /// </summary>
        public HttpMessageHandler? HttpMessageHandler { get; set; }
    }

    /// <summary>Options for a single request, overriding the client's.</summary>
    public class RequestOptions
    {
        /// <summary>Overrides <see cref="AnvilClientOptions.Timeout"/> for this request.</summary>
        public TimeSpan? Timeout { get; set; }
    }
}
