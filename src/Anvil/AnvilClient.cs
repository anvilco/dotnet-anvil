using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Anvil
{
    /// <summary>
    /// The Anvil API client. One instance per API key or access token; it is
    /// thread-safe, and paces requests to the API's rate limit.
    /// </summary>
    /// <example>
    /// <code>
    /// var anvil = new AnvilClient(new AnvilClientOptions { ApiKey = "..." });
    /// var pdf = await anvil.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/generate-pdf", new { data = new[] { new { content = "Hello" } } });
    /// </code>
    /// </example>
    public partial class AnvilClient : IDisposable
    {
        /// <summary>A <c>versionNumber</c> for the latest version of a PDF template, published or not.</summary>
        public const int VERSION_LATEST = -1;

        /// <summary>
        /// A <c>versionNumber</c> for the latest published version of a PDF template.
        /// The default when no version is given, except that it is an error when the
        /// template has never been published.
        /// </summary>
        public const int VERSION_LATEST_PUBLISHED = -2;

        private static readonly int[] RetriedStatuses = { 500, 502, 503, 504 };

        /// <summary>
        /// How payloads are serialized: camelCase property names, nulls left out.
        /// </summary>
        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new GraphQLUploadConverter() },
        };

        internal static readonly JsonSerializer Serializer = JsonSerializer.Create(SerializerSettings);

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly RateLimiter _limiter;
        private readonly string _authorization;
        private bool _disposed;

        /// <summary>Creates a client with an API key, for production or the environment at <paramref name="baseUrl"/>.</summary>
        public AnvilClient(string apiKey, string? baseUrl = null)
            : this(new AnvilClientOptions { ApiKey = apiKey, BaseUrl = baseUrl ?? Constants.DefaultBaseUrl })
        {
        }

        public AnvilClient(AnvilClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Options = options;
            BaseUrl = Constants.NormalizeBaseUrl(options.BaseUrl);
            UserAgent = string.IsNullOrWhiteSpace(options.UserAgent) ? $"dotnet-anvil/{Version}" : options.UserAgent!;

            var apiKey = options.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(options.AccessToken))
            {
                apiKey = Environment.GetEnvironmentVariable("ANVIL_API_KEY");
            }

            _authorization = Auth.Header(apiKey, options.AccessToken);
            _limiter = RateLimiter.Shared(BaseUrl, _authorization, options.RequestLimit, options.RequestLimitMs);

            if (options.HttpClient != null)
            {
                _http = options.HttpClient;
            }
            else
            {
                _http = options.HttpMessageHandler != null ? new HttpClient(options.HttpMessageHandler, disposeHandler: false) : new HttpClient();
                // The client applies its own timeout per request
                _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                _ownsHttp = true;
            }
        }

        /// <summary>The options the client was created with.</summary>
        public AnvilClientOptions Options { get; }

        /// <summary>The environment's base URL, without a trailing slash.</summary>
        public string BaseUrl { get; }

        /// <summary>The <c>User-Agent</c> header sent with every request.</summary>
        public string UserAgent { get; }

        /// <summary>The rate limit requests are paced to, and whether it came from the API.</summary>
        public RateLimitInfo RateLimit => _limiter.Info;

        /// <summary>
        /// Raised when the API answered <c>429</c> and the request is about to be
        /// retried. The client paces requests so this shouldn't happen; another
        /// process using the same API key at the same time can cause it.
        /// </summary>
        public event EventHandler<RateLimitedEventArgs>? RateLimited;

        /// <summary>The client's version, as in its <c>User-Agent</c>.</summary>
        public static string Version { get; } = ReadVersion();

        private static string ReadVersion()
        {
            var version = typeof(AnvilClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(AnvilClient).Assembly.GetName().Version?.ToString()
                ?? "unknown";
            var plus = version.IndexOf('+');
            return plus > 0 ? version.Substring(0, plus) : version;
        }

        // ------------------------------------------------------------------
        // URLs

        /// <summary>
        /// The full URL of a path on the client's base URL. A full URL is accepted
        /// if it is on the base URL, and refused otherwise, so credentials never go
        /// to another host.
        /// </summary>
        public string Url(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) || !SameOrigin(uri, new Uri(BaseUrl)))
                {
                    throw new ArgumentException($"`{path}` is not on the client's base URL {BaseUrl}", nameof(path));
                }

                return path;
            }

            return BaseUrl + "/" + path.TrimStart('/');
        }

        private static bool SameOrigin(Uri a, Uri b)
            => string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
               && a.Port == b.Port;

        internal static string WithQuery(string url, IDictionary<string, string>? query)
        {
            if (query == null || query.Count == 0)
            {
                return url;
            }

            var parameters = string.Join("&", query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value ?? "")}"));
            return url + (url.Contains("?") ? "&" : "?") + parameters;
        }

        // ------------------------------------------------------------------
        // GraphQL

        /// <summary>
        /// Sends any GraphQL query or mutation and returns its <c>data</c>. Throws
        /// <see cref="AnvilClientException"/> when the response has <c>errors</c>.
        /// </summary>
        /// <param name="query">The operation, e.g. <c>query { currentUser { eid } }</c></param>
        /// <param name="variables">Its variables: an anonymous object, a dictionary, a payload type or a <see cref="JObject"/></param>
        /// <param name="operationName">Which operation to run, when the document has several</param>
        public async Task<JObject> RequestGraphQLAsync(
            string query,
            object? variables = null,
            string? operationName = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var processed = await SendGraphQLAsync(query, variables, operationName, options, cancellationToken).ConfigureAwait(false);
            return (processed.RequireJson() as JObject)?["data"] as JObject ?? new JObject();
        }

        /// <summary>Like <see cref="RequestGraphQLAsync(string, object?, string?, RequestOptions?, CancellationToken)"/>, with the <c>data</c> deserialized to <typeparamref name="T"/>.</summary>
        public async Task<T> RequestGraphQLAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var data = await RequestGraphQLAsync(query, variables, operationName, options, cancellationToken).ConfigureAwait(false);
            return data.ToObject<T>(Serializer)!;
        }

        internal Task<ProcessedResponse> SendGraphQLAsync(string query, object? variables, string? operationName, RequestOptions? options, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("query is required", nameof(query));
            }

            var operation = new JObject { ["query"] = query };
            if (variables != null)
            {
                operation["variables"] = variables as JToken ?? JToken.FromObject(variables, Serializer);
            }

            if (operationName != null)
            {
                operation["operationName"] = operationName;
            }

            return SendGraphQLOperationAsync(operation, options, cancellationToken);
        }

        /// <summary>
        /// Sends a GraphQL operation as JSON, or as a multipart request when its
        /// variables hold files from <see cref="PrepareGraphQLFile(string, string?, string?)"/>.
        /// </summary>
        internal Task<ProcessedResponse> SendGraphQLOperationAsync(JObject operation, RequestOptions? options, CancellationToken cancellationToken)
        {
            Uploads.Validate(operation);
            var uploads = Uploads.Extract(operation);
            var url = Url("/graphql");
            if (uploads.Count == 0)
            {
                var body = operation.ToString(Formatting.None);
                return SendAndProcessAsync(
                    () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(body) },
                    stream: false,
                    options,
                    cancellationToken);
            }

            return SendAndProcessAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url) { Content = Uploads.Build(operation, uploads) },
                stream: false,
                options,
                cancellationToken);
        }

        // ------------------------------------------------------------------
        // REST

        /// <summary>
        /// Sends any REST request and returns its JSON. <paramref name="path"/> is
        /// on the base URL, e.g. <c>/api/v1/generate-pdf</c>. Throws
        /// <see cref="AnvilClientException"/> on an error status, and
        /// <see cref="InvalidOperationException"/> when the response isn't JSON (use
        /// <see cref="RequestRestBytesAsync"/> or <see cref="RequestRestStreamAsync"/>).
        /// </summary>
        /// <param name="json">The request body, serialized as JSON</param>
        /// <param name="query">Query string parameters</param>
        public async Task<JToken> RequestRestAsync(
            HttpMethod method,
            string path,
            object? json = null,
            IDictionary<string, string>? query = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var processed = await SendRestCoreAsync(method, path, JsonContentFactory(json), query, stream: false, options, cancellationToken).ConfigureAwait(false);
            return processed.RequireJson();
        }

        /// <summary>Like <see cref="RequestRestAsync"/>, with the JSON deserialized to <typeparamref name="T"/>.</summary>
        public async Task<T> RequestRestAsync<T>(
            HttpMethod method,
            string path,
            object? json = null,
            IDictionary<string, string>? query = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var token = await RequestRestAsync(method, path, json, query, options, cancellationToken).ConfigureAwait(false);
            return token.ToObject<T>(Serializer)!;
        }

        /// <summary>Sends any REST request and returns the response body, e.g. a PDF.</summary>
        public async Task<byte[]> RequestRestBytesAsync(
            HttpMethod method,
            string path,
            object? json = null,
            IDictionary<string, string>? query = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var processed = await SendRestCoreAsync(method, path, JsonContentFactory(json), query, stream: false, options, cancellationToken).ConfigureAwait(false);
            return await processed.ReadBytesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Sends any REST request and returns the response body as a stream, to
        /// read a large PDF or zip without holding it in memory. Dispose the stream
        /// when done.
        /// </summary>
        public async Task<Stream> RequestRestStreamAsync(
            HttpMethod method,
            string path,
            object? json = null,
            IDictionary<string, string>? query = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var processed = await SendRestCoreAsync(method, path, JsonContentFactory(json), query, stream: true, options, cancellationToken).ConfigureAwait(false);
            return await processed.ReadStreamAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a REST request with any content and returns the response, after
        /// checking it for errors. The content is created per attempt, since a
        /// request may be sent more than once.
        /// </summary>
        public async Task<HttpResponseMessage> SendRestAsync(
            HttpMethod method,
            string path,
            Func<HttpContent?>? content = null,
            IDictionary<string, string>? query = null,
            RequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var processed = await SendRestCoreAsync(method, path, content, query, stream: false, options, cancellationToken).ConfigureAwait(false);
            return processed.Response;
        }

        private Task<ProcessedResponse> SendRestCoreAsync(
            HttpMethod method,
            string path,
            Func<HttpContent?>? content,
            IDictionary<string, string>? query,
            bool stream,
            RequestOptions? options,
            CancellationToken cancellationToken)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            var url = WithQuery(Url(path), query);
            return SendAndProcessAsync(
                () => new HttpRequestMessage(method, url) { Content = content?.Invoke() },
                stream,
                options,
                cancellationToken);
        }

        private static Func<HttpContent?>? JsonContentFactory(object? json)
        {
            if (json == null)
            {
                return null;
            }

            var body = json is JToken token ? token.ToString(Formatting.None) : JsonConvert.SerializeObject(json, SerializerSettings);
            return () => JsonContent(body);
        }

        internal static StringContent JsonContent(string body) => new StringContent(body, Encoding.UTF8, "application/json");

        // ------------------------------------------------------------------
        // Transport: rate limiting, retries and errors

        internal async Task<ProcessedResponse> SendAndProcessAsync(Func<HttpRequestMessage> makeRequest, bool stream, RequestOptions? options, CancellationToken cancellationToken)
        {
            var response = await SendWithRetriesAsync(makeRequest, stream, options, cancellationToken).ConfigureAwait(false);
            return await ProcessedResponse.CreateAsync(response, stream).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a request through the rate limiter, retrying a <c>429</c> after its
        /// <c>Retry-After</c>, and a <c>5xx</c> or connection failure with backoff
        /// up to <see cref="AnvilClientOptions.MaxRetries"/> times. Returns the
        /// response whatever its status.
        /// </summary>
        internal async Task<HttpResponseMessage> SendWithRetriesAsync(Func<HttpRequestMessage> makeRequest, bool stream, RequestOptions? options, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AnvilClient));
            }

            var timeout = options?.Timeout ?? Options.Timeout;
            var completion = stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            var maxRetries = Math.Max(0, Options.MaxRetries);

            for (var attempt = 0; ; attempt++)
            {
                await _limiter.AcquireAsync(cancellationToken).ConfigureAwait(false);

                HttpResponseMessage response;
                var request = makeRequest();
                try
                {
                    request.Headers.TryAddWithoutValidation("Authorization", _authorization);
                    request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                    if (request.Headers.Accept.Count == 0)
                    {
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    }

                    using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        if (timeout > TimeSpan.Zero && timeout != System.Threading.Timeout.InfiniteTimeSpan)
                        {
                            timeoutSource.CancelAfter(timeout);
                        }

                        try
                        {
                            response = await _http.SendAsync(request, completion, timeoutSource.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // The request was sent, so the API may have processed it: not retried
                            throw new TimeoutException($"The request to {request.RequestUri} timed out after {timeout}");
                        }
                    }
                }
                catch (HttpRequestException ex) when (attempt < maxRetries && IsConnectionFailure(ex))
                {
                    _limiter.ReleaseFailed();
                    request.Dispose();
                    await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (Exception)
                {
                    _limiter.ReleaseFailed();
                    request.Dispose();
                    throw;
                }

                _limiter.Release(HeaderInt(response, RateLimiter.LimitHeader), HeaderInt(response, RateLimiter.IntervalHeader));

                var status = (int)response.StatusCode;
                if (status == 429)
                {
                    var retryAfter = RetryAfter(response) ?? Constants.DefaultRetryAfterMs;
                    var wait = TimeSpan.FromMilliseconds(Math.Min(retryAfter, Constants.MaxRetryAfterMs) + Constants.RateLimitBufferMs);
                    response.Dispose();
                    RateLimited?.Invoke(this, new RateLimitedEventArgs(wait));
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (RetriedStatuses.Contains(status) && attempt < maxRetries)
                {
                    var retryAfter = RetryAfter(response);
                    var wait = retryAfter.HasValue ? Math.Min(retryAfter.Value, Constants.MaxRetryAfterMs) : Backoff(attempt);
                    response.Dispose();
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
        }

        private static int Backoff(int attempt) => Constants.RetryBackoffBaseMs * (1 << Math.Min(attempt, 20));

        private static bool IsConnectionFailure(Exception exception)
        {
            for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is SocketException)
                {
                    return true;
                }
            }

            return false;
        }

        private static int? HeaderInt(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                var value = values.FirstOrDefault();
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                {
                    return parsed;
                }
            }

            return null;
        }

        /// <summary>The <c>Retry-After</c> header in milliseconds, when it is a number of seconds.</summary>
        private static int? RetryAfter(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                var value = values.FirstOrDefault();
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return (int)Math.Round(Math.Abs(seconds) * 1000);
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_ownsHttp)
                {
                    _http.Dispose();
                }
            }
        }
    }

    /// <summary>A response that has been checked for errors.</summary>
    internal sealed class ProcessedResponse
    {
        private ProcessedResponse(HttpResponseMessage response, bool isJson, string? body, JToken? json)
        {
            Response = response;
            IsJson = isJson;
            Body = body;
            Json = json;
        }

        public HttpResponseMessage Response { get; }

        /// <summary>Whether the response's content type is JSON.</summary>
        public bool IsJson { get; }

        /// <summary>The body, when it has been read (a JSON response, or an error).</summary>
        public string? Body { get; }

        /// <summary>The parsed body of a JSON response.</summary>
        public JToken? Json { get; }

        public int StatusCode => (int)Response.StatusCode;

        /// <summary>
        /// Reads the response and throws <see cref="AnvilClientException"/> when it is
        /// an error: a status outside 2xx, a JSON body with <c>errors</c>, or a body
        /// that claims to be JSON but isn't.
        /// </summary>
        public static async Task<ProcessedResponse> CreateAsync(HttpResponseMessage response, bool stream)
        {
            var status = (int)response.StatusCode;
            var mediaType = response.Content?.Headers.ContentType?.MediaType ?? "";
            var isJson = mediaType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;

            string? body = null;
            JToken? json = null;
            Exception? parseError = null;
            if (status >= 300 || isJson)
            {
                body = response.Content == null ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (isJson)
                {
                    try
                    {
                        json = body.Trim().Length == 0 ? null : JToken.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        parseError = ex;
                    }
                }
            }

            var hasGraphqlErrors = json is JObject obj && obj["errors"] is JArray errorsArray && errorsArray.Count > 0;
            if (status < 300 && parseError == null && !hasGraphqlErrors)
            {
                return new ProcessedResponse(response, isJson, body, json);
            }

            var reason = string.IsNullOrEmpty(response.ReasonPhrase) ? ((HttpStatusCode)status).ToString() : response.ReasonPhrase;
            IReadOnlyList<AnvilError> errors;
            if (parseError != null)
            {
                errors = Errors.FromException(parseError);
            }
            else if (isJson && json != null)
            {
                errors = Errors.NormalizeJson(json, reason);
            }
            else
            {
                errors = Errors.FromStatus(reason, body);
            }

            throw new AnvilClientException(errors, status, Errors.Headers(response), body, (json as JObject)?["data"], response, parseError);
        }

        /// <summary>The JSON body, or <see cref="InvalidOperationException"/> when the response isn't JSON.</summary>
        public JToken RequireJson()
        {
            if (!IsJson)
            {
                var mediaType = Response.Content?.Headers.ContentType?.MediaType ?? "no content type";
                throw new InvalidOperationException($"Expected a JSON response but got {mediaType}. Use RequestRestBytesAsync or RequestRestStreamAsync for a PDF or zip.");
            }

            return Json ?? new JObject();
        }

        public async Task<byte[]> ReadBytesAsync()
        {
            if (Body != null)
            {
                return Encoding.UTF8.GetBytes(Body);
            }

            return Response.Content == null
                ? Array.Empty<byte>()
                : await Response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public async Task<Stream> ReadStreamAsync()
        {
            if (Body != null)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(Body));
            }

            if (Response.Content == null)
            {
                return new MemoryStream();
            }

            var content = await Response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return new ResponseStream(Response, content);
        }

        /// <summary>The response's body stream, which disposes the response with it.</summary>
        private sealed class ResponseStream : Stream
        {
            private readonly HttpResponseMessage _response;
            private readonly Stream _inner;

            public ResponseStream(HttpResponseMessage response, Stream inner)
            {
                _response = response;
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    _response.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
