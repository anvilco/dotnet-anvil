using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Anvil;

using AnvilTests.Support;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    public class AnvilClientTests
    {
        private static string Base64(string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value));

        // ------------------------------------------------------------------
        // Options and URLs

        [Fact]
        public void BaseUrl_DefaultsToProduction_AndDropsATrailingSlash()
        {
            using var production = Clients.Create(new FakeHandler());
            using var staging = Clients.Create(new FakeHandler(), o => o.BaseUrl = "https://staging.useanvil.com/");

            Assert.Equal("https://app.useanvil.com", production.BaseUrl);
            Assert.Equal("https://staging.useanvil.com", staging.BaseUrl);
            Assert.Equal("https://staging.useanvil.com/graphql", staging.Url("/graphql"));
            Assert.Equal("https://staging.useanvil.com/api/v1/fill/abc.pdf", staging.Url("api/v1/fill/abc.pdf"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("app.useanvil.com")]
        [InlineData("ftp://app.useanvil.com")]
        public void BaseUrl_MustBeAnHttpUrl(string baseUrl)
        {
            if (baseUrl == "")
            {
                // Empty means the default
                using var client = Clients.Create(new FakeHandler(), o => o.BaseUrl = baseUrl);
                Assert.Equal("https://app.useanvil.com", client.BaseUrl);
                return;
            }

            Assert.Throws<ArgumentException>(() => Clients.Create(new FakeHandler(), o => o.BaseUrl = baseUrl));
        }

        [Fact]
        public void Url_AcceptsAFullUrlOnTheBase_AndRefusesAnotherHost()
        {
            using var client = Clients.Create(new FakeHandler(), o => o.BaseUrl = "https://staging.useanvil.com");

            Assert.Equal("https://staging.useanvil.com/api/v1/x", client.Url("https://staging.useanvil.com/api/v1/x"));
            var ex = Assert.Throws<ArgumentException>(() => client.Url("https://evil.example.com/api/v1/x"));
            Assert.Contains("not on the client's base URL", ex.Message);
        }

        [Fact]
        public async Task Requests_GoToTheBaseUrl()
        {
            var handler = new FakeHandler()
                .RespondJson(@"{""data"":{""ok"":true}}", ("X-RateLimit-Limit", "100"), ("X-RateLimit-Interval-Ms", "1000"))
                .RespondPdf().RespondPdf().RespondJson("{}");
            using var client = Clients.Create(handler, o => o.BaseUrl = "https://staging.useanvil.com");

            await client.RequestGraphQLAsync("{ currentUser { eid } }");
            await client.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/fill/cast1.pdf", new { data = new { } }, new Dictionary<string, string> { ["versionNumber"] = "-1" });
            await client.RequestRestBytesAsync(HttpMethod.Get, "/api/document-group/group1.zip");
            await client.RequestRestAsync(HttpMethod.Get, "api/v1/something?a=1", query: new Dictionary<string, string> { ["b"] = "two words" });

            Assert.Equal("https://staging.useanvil.com/graphql", handler.Requests[0].Uri.AbsoluteUri);
            Assert.Equal("https://staging.useanvil.com/api/v1/fill/cast1.pdf?versionNumber=-1", handler.Requests[1].Uri.AbsoluteUri);
            Assert.Equal("https://staging.useanvil.com/api/document-group/group1.zip", handler.Requests[2].Uri.AbsoluteUri);
            Assert.Equal("https://staging.useanvil.com/api/v1/something?a=1&b=two%20words", handler.Requests[3].Uri.AbsoluteUri);
        }

        // ------------------------------------------------------------------
        // Headers

        [Fact]
        public async Task Requests_CarryBasicAuthWithATrailingColon_AndAUserAgent()
        {
            var handler = new FakeHandler().RespondJson("{}");
            using var client = Clients.Create(handler, o => o.ApiKey = "my-api-key");

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal($"Basic {Base64("my-api-key:")}", handler.LastRequest.Headers["Authorization"]);
            Assert.Equal($"dotnet-anvil/{AnvilClient.Version}", handler.LastRequest.Headers["User-Agent"]);
            Assert.Matches(@"^\d+\.\d+\.\d+", AnvilClient.Version);
            Assert.Equal("application/json", handler.LastRequest.Headers["Accept"]);
        }

        [Fact]
        public async Task Requests_CarryBearerAuth_WhenGivenAnAccessToken()
        {
            var handler = new FakeHandler().RespondJson("{}");
            using var client = Clients.Create(handler, o =>
            {
                o.ApiKey = null;
                o.AccessToken = "token-" + Guid.NewGuid().ToString("N");
            });

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal($"Bearer {Base64(client.Options.AccessToken!)}", handler.LastRequest.Headers["Authorization"]);
        }

        [Fact]
        public async Task UserAgent_CanBeOverridden()
        {
            var handler = new FakeHandler().RespondJson("{}");
            using var client = Clients.Create(handler, o => o.UserAgent = "my-app/2.0");

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal("my-app/2.0", handler.LastRequest.Headers["User-Agent"]);
        }

        // ------------------------------------------------------------------
        // GraphQL

        [Fact]
        public async Task RequestGraphQL_PostsTheOperation_AndReturnsData()
        {
            var handler = new FakeHandler().RespondJson(@"{""data"":{""currentUser"":{""eid"":""user1""}}}");
            using var client = Clients.Create(handler);

            var data = await client.RequestGraphQLAsync("query Me($eid: String!) { currentUser(eid: $eid) { eid } }", new { Eid = "user1" }, "Me");

            Assert.Equal("user1", data["currentUser"]!["eid"]!.ToString());
            var body = JObject.Parse(handler.LastRequest.Body!);
            Assert.Equal("query Me($eid: String!) { currentUser(eid: $eid) { eid } }", body["query"]!.ToString());
            Assert.Equal("user1", body["variables"]!["eid"]!.ToString());
            Assert.Equal("Me", body["operationName"]!.ToString());
            Assert.StartsWith("application/json", handler.LastRequest.ContentType);
        }

        [Fact]
        public async Task RequestGraphQL_DeserializesToAType()
        {
            var handler = new FakeHandler().RespondJson(@"{""data"":{""etchPacket"":{""eid"":""packet1"",""name"":""Packet""}}}");
            using var client = Clients.Create(handler);

            var data = await client.RequestGraphQLAsync<Dictionary<string, Anvil.Types.GraphQL.EtchPacket>>("{ etchPacket { eid name } }");

            Assert.Equal("Packet", data["etchPacket"].Name);
        }

        [Fact]
        public async Task RequestGraphQL_WithoutData_ReturnsAnEmptyObject()
        {
            var handler = new FakeHandler().RespondJson(@"{}");
            using var client = Clients.Create(handler);

            var data = await client.RequestGraphQLAsync("{ x }");

            Assert.Empty(data);
        }

        [Fact]
        public async Task RequestGraphQL_Errors_ThrowWithPartialData()
        {
            var handler = new FakeHandler().RespondJson(@"{
                ""data"": { ""currentUser"": null },
                ""errors"": [{ ""message"": ""Not authorized"", ""name"": ""AuthorizationError"", ""path"": [""currentUser""] }]
            }");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ currentUser { eid } }"));

            Assert.Equal(200, ex.StatusCode);
            Assert.Equal("200: AuthorizationError: Not authorized", ex.Message);
            Assert.Equal("AuthorizationError", Assert.Single(ex.Errors).Name);
            Assert.Equal(JTokenType.Null, ex.ResponseData!["currentUser"]!.Type);
            Assert.Equal("Not authorized", ex.Data["Message1"]);
        }

        // ------------------------------------------------------------------
        // REST responses

        [Fact]
        public async Task RequestRest_SendsJson_AndParsesJson()
        {
            var handler = new FakeHandler().RespondJson(@"{""url"":""https://example.com/x.pdf""}");
            using var client = Clients.Create(handler);

            var json = await client.RequestRestAsync(HttpMethod.Post, "/api/v1/fill/cast1.pdf", new { Title = "T", data = new { Name = "Sally" }, outputFormat = "url" });

            Assert.Equal("https://example.com/x.pdf", json["url"]!.ToString());
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            var body = JObject.Parse(handler.LastRequest.Body!);
            Assert.Equal("T", body["title"]!.ToString());
            Assert.Equal("Sally", body["data"]!["name"]!.ToString());
        }

        [Fact]
        public async Task RequestRest_RefusesABinaryResponse()
        {
            var handler = new FakeHandler().RespondPdf();
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Contains("application/pdf", ex.Message);
        }

        [Fact]
        public async Task RequestRestBytes_ReturnsTheBody_BinaryOrJson()
        {
            var handler = new FakeHandler().RespondPdf().RespondJson(@"{""a"":1}");
            using var client = Clients.Create(handler);

            Assert.Equal(FakeHandler.Pdf, await client.RequestRestBytesAsync(HttpMethod.Get, "/api/v1/x"));
            Assert.Equal(@"{""a"":1}", Encoding.UTF8.GetString(await client.RequestRestBytesAsync(HttpMethod.Get, "/api/v1/x")));
        }

        [Fact]
        public async Task RequestRestStream_ReturnsTheBody_BinaryOrJson()
        {
            var handler = new FakeHandler().RespondPdf().RespondJson(@"{""a"":1}");
            using var client = Clients.Create(handler);

            using (var stream = await client.RequestRestStreamAsync(HttpMethod.Get, "/api/v1/x"))
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                Assert.Equal(FakeHandler.Pdf, buffer.ToArray());
            }

            using (var stream = await client.RequestRestStreamAsync(HttpMethod.Get, "/api/v1/x"))
            using (var reader = new StreamReader(stream))
            {
                Assert.Equal(@"{""a"":1}", await reader.ReadToEndAsync());
            }
        }

        [Fact]
        public async Task SendRest_ReturnsTheCheckedResponse_WithAnyContent()
        {
            var handler = new FakeHandler().RespondPdf();
            using var client = Clients.Create(handler);

            var response = await client.SendRestAsync(HttpMethod.Post, "/api/v1/x", () => new StringContent("raw", Encoding.UTF8, "text/plain"));

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal(FakeHandler.Pdf, await response.Content.ReadAsByteArrayAsync());
            Assert.Equal("raw", handler.LastRequest.Body);
        }

        // ------------------------------------------------------------------
        // Errors

        [Fact]
        public async Task RestValidationError_IsStructured()
        {
            var handler = new FakeHandler().Respond(400, @"{""name"":""ValidationError"",""message"":""Invalid"",""fields"":[{""property"":""data"",""message"":""Required""}]}");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/fill/x.pdf", new { }));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("400: ValidationError: Invalid (data: Required)", ex.Message);
            Assert.Equal("data", Assert.Single(Assert.Single(ex.Errors).Fields).Property);
            Assert.Contains("ValidationError", ex.ResponseContent);
            Assert.Equal("application/json; charset=utf-8", ex.Headers["Content-Type"].Single());
            Assert.NotNull(ex.Response);
        }

        [Fact]
        public async Task NonJsonError_KeepsTheBody()
        {
            var handler = new FakeHandler().Respond(502, "<html>Bad Gateway from the proxy</html>", "text/html");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));

            Assert.Equal(502, ex.StatusCode);
            Assert.Equal("502: Bad Gateway", ex.Message);
            Assert.Equal("<html>Bad Gateway from the proxy</html>", ex.ResponseContent);
            Assert.Equal("<html>Bad Gateway from the proxy</html>", Assert.Single(ex.Errors).Raw!["body"]!.ToString());
        }

        [Fact]
        public async Task EmptyError_IsUnknown()
        {
            var handler = new FakeHandler().Respond(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { ReasonPhrase = "" });
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));

            Assert.Equal(500, ex.StatusCode);
            Assert.Equal("500: InternalServerError", ex.Message);
        }

        [Fact]
        public async Task JsonThatCannotBeParsed_IsAnError()
        {
            var handler = new FakeHandler().RespondJson(@"{""data"": {""cut"": ");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestGraphQLAsync("{ x }"));

            Assert.Equal(200, ex.StatusCode);
            Assert.Equal("JsonReaderException", Assert.Single(ex.Errors).Name);
            Assert.IsType<Newtonsoft.Json.JsonReaderException>(ex.InnerException);
        }

        [Fact]
        public async Task EmptyJsonSuccess_IsNotAnError()
        {
            var handler = new FakeHandler().RespondJson("");
            using var client = Clients.Create(handler);

            Assert.Empty(await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));
        }

        [Fact]
        public async Task ASuccessWithAName_IsNotAnError()
        {
            var handler = new FakeHandler().RespondJson(@"{""name"":""My Template"",""eid"":""cast1""}");
            using var client = Clients.Create(handler);

            var json = await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal("cast1", json["eid"]!.ToString());
        }

        [Fact]
        public async Task NetworkFailure_IsNotWrapped()
        {
            var handler = new FakeHandler().Throw(new HttpRequestException("boom"));
            using var client = Clients.Create(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.RequestGraphQLAsync("{ x }"));
        }

        // ------------------------------------------------------------------
        // Rate limiting and retries

        [Fact]
        public async Task TheLimit_IsLearnedFromTheFirstResponse()
        {
            var handler = new FakeHandler().RespondJson("{}", ("X-RateLimit-Limit", "40"), ("X-RateLimit-Interval-Ms", "5000"));
            using var client = Clients.Create(handler);

            Assert.False(client.RateLimit.LearnedFromResponse);
            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.True(client.RateLimit.LearnedFromResponse);
            Assert.Equal(40, client.RateLimit.Limit);
            Assert.Equal(5000, client.RateLimit.IntervalMs);
        }

        [Fact]
        public async Task ConcurrentRequests_AreHeldUntilTheFirstResponse_AndPaced()
        {
            var handler = new FakeHandler().RespondJson("{}", ("X-RateLimit-Limit", "2"), ("X-RateLimit-Interval-Ms", "300"));
            using var client = Clients.Create(handler);

            var clock = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x")));

            // 4 requests at 2 per 300ms: the first alone, then one more, then the window
            Assert.Equal(4, handler.Requests.Count);
            Assert.True(clock.ElapsedMilliseconds >= 300, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task A429_IsRetriedAfterRetryAfterPlusABuffer()
        {
            var handler = new FakeHandler()
                .Respond(429, "Rate limit exceeded", "text/plain", ("Retry-After", "0.2"))
                .RespondJson(@"{""data"":{""ok"":true}}");
            using var client = Clients.Create(handler);
            var waits = new List<TimeSpan>();
            client.RateLimited += (_, e) => waits.Add(e.RetryAfter);

            var clock = Stopwatch.StartNew();
            var data = await client.RequestGraphQLAsync("{ ok }");

            Assert.True((bool)data["ok"]!);
            Assert.Equal(2, handler.Requests.Count);
            Assert.True(clock.ElapsedMilliseconds >= 240, $"took {clock.ElapsedMilliseconds}ms");
            Assert.Equal(TimeSpan.FromMilliseconds(250), Assert.Single(waits));
        }

        [Fact]
        public async Task A429_WithoutRetryAfter_WaitsASecond()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain")
                .RespondJson("{}");
            using var client = Clients.Create(handler);
            var waits = new List<TimeSpan>();
            client.RateLimited += (_, e) => waits.Add(e.RetryAfter);

            var clock = Stopwatch.StartNew();
            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(2, handler.Requests.Count);
            Assert.True(clock.ElapsedMilliseconds >= 1000, $"took {clock.ElapsedMilliseconds}ms");
            Assert.Equal(TimeSpan.FromMilliseconds(1050), Assert.Single(waits));
        }

        [Fact]
        public async Task Repeated429s_KeepRetrying()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .RespondJson("{}");
            using var client = Clients.Create(handler);

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(4, handler.Requests.Count);
        }

        [Fact]
        public async Task A429_SetsTheLimitToo()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain", ("Retry-After", "0"), ("X-RateLimit-Limit", "9"), ("X-RateLimit-Interval-Ms", "900"))
                .RespondJson("{}");
            using var client = Clients.Create(handler);

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(9, client.RateLimit.Limit);
        }

        [Fact]
        public async Task A5xx_IsRetriedWithBackoff_UpToMaxRetries()
        {
            var handler = new FakeHandler()
                .Respond(502, "bad", "text/plain")
                .Respond(503, "bad", "text/plain")
                .RespondJson("{}");
            using var client = Clients.Create(handler, o => o.MaxRetries = 2);

            var clock = Stopwatch.StartNew();
            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(3, handler.Requests.Count);
            // 100ms then 200ms
            Assert.True(clock.ElapsedMilliseconds >= 280, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task A5xx_PastMaxRetries_Throws()
        {
            var handler = new FakeHandler().Respond(500, "bad", "text/plain");
            using var client = Clients.Create(handler, o => o.MaxRetries = 1);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Equal(500, ex.StatusCode);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task A5xx_WithMaxRetriesZero_IsNotRetried()
        {
            var handler = new FakeHandler().Respond(503, "bad", "text/plain");
            using var client = Clients.Create(handler, o => o.MaxRetries = 0);

            await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task A5xx_HonorsRetryAfter()
        {
            var handler = new FakeHandler()
                .Respond(503, "bad", "text/plain", ("Retry-After", "0.3"))
                .RespondJson("{}");
            using var client = Clients.Create(handler, o => o.MaxRetries = 1);

            var clock = Stopwatch.StartNew();
            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.True(clock.ElapsedMilliseconds >= 280, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task A400_IsNotRetried()
        {
            var handler = new FakeHandler().Respond(400, @"{""message"":""bad""}");
            using var client = Clients.Create(handler, o => o.MaxRetries = 5);

            await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task AConnectionFailure_IsRetried()
        {
            var handler = new FakeHandler()
                .Throw(new HttpRequestException("connect", new SocketException((int)SocketError.ConnectionRefused)))
                .RespondJson("{}");
            using var client = Clients.Create(handler, o => o.MaxRetries = 1);

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task AConnectionFailure_PastMaxRetries_Throws()
        {
            var handler = new FakeHandler().Throw(new HttpRequestException("connect", new SocketException((int)SocketError.ConnectionRefused)));
            using var client = Clients.Create(handler, o => o.MaxRetries = 2);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Equal(3, handler.Requests.Count);
        }

        [Fact]
        public async Task AFailedFirstRequest_DoesNotHangTheNext()
        {
            var handler = new FakeHandler().Throw(new HttpRequestException("boom")).RespondJson("{}");
            using var client = Clients.Create(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));
            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x").WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task ARequestBody_IsSentAgainOnRetry()
        {
            var handler = new FakeHandler()
                .Respond(429, "", "text/plain", ("Retry-After", "0"))
                .RespondJson("{}");
            using var client = Clients.Create(handler);

            await client.RequestRestAsync(HttpMethod.Post, "/api/v1/x", new { a = 1 });

            Assert.Equal(@"{""a"":1}", handler.Requests[0].Body);
            Assert.Equal(@"{""a"":1}", handler.Requests[1].Body);
        }

        [Fact]
        public async Task ClientsWithTheSameKey_ShareALimiter()
        {
            var handler = new FakeHandler().RespondJson("{}", ("X-RateLimit-Limit", "12"), ("X-RateLimit-Interval-Ms", "1200"));
            var key = "shared-" + Guid.NewGuid().ToString("N");
            using var a = Clients.Create(handler, o => o.ApiKey = key);
            using var b = Clients.Create(handler, o => o.ApiKey = key);

            await a.RequestRestAsync(HttpMethod.Get, "/api/v1/x");

            Assert.True(b.RateLimit.LearnedFromResponse);
            Assert.Equal(12, b.RateLimit.Limit);
        }

        // ------------------------------------------------------------------
        // Timeouts

        [Fact]
        public async Task ARequestThatTimesOut_ThrowsTimeout_AndIsNotRetried()
        {
            var handler = new FakeHandler().RespondAfter(300);
            using var client = Clients.Create(handler, o =>
            {
                o.Timeout = TimeSpan.FromMilliseconds(50);
                o.MaxRetries = 3;
            });

            await Assert.ThrowsAsync<TimeoutException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));

            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task ThePerRequestTimeout_OverridesTheClients()
        {
            var handler = new FakeHandler().RespondAfter(200);
            using var client = Clients.Create(handler, o => o.Timeout = TimeSpan.FromMilliseconds(50));

            await client.RequestRestAsync(HttpMethod.Get, "/api/v1/x", options: new RequestOptions { Timeout = TimeSpan.FromSeconds(5) });
        }

        [Fact]
        public async Task Cancellation_IsTheCallersException()
        {
            var handler = new FakeHandler().RespondAfter(300);
            using var client = Clients.Create(handler);
            using var cancel = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x", cancellationToken: cancel.Token));
        }

        // ------------------------------------------------------------------
        // The pre-1.0 clients

        [Fact]
        public async Task TheOldClients_GoThroughAnvilClient()
        {
#pragma warning disable CS0618
            using var rest = new Anvil.Client.RestClient("old-key-" + Guid.NewGuid().ToString("N"), "https://staging.useanvil.com");
            using var graphql = new Anvil.Client.GraphQLClient("old-key-" + Guid.NewGuid().ToString("N"), "https://staging.useanvil.com");
#pragma warning restore CS0618

            Assert.Equal("https://staging.useanvil.com", rest.Anvil.BaseUrl);
            Assert.Equal("https://staging.useanvil.com", graphql.Anvil.BaseUrl);
            Assert.Equal(ClientConstants.VERSION_LATEST, AnvilClient.VERSION_LATEST);
            Assert.Equal(ClientConstants.VERSION_LATEST_PUBLISHED, AnvilClient.VERSION_LATEST_PUBLISHED);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task Issue17_A429WithANonJsonBody_DoesNotCrash()
        {
            // The pre-1.0 client threw a JSON parsing error on a 429 with a text body.
            // Now the 429 is retried, and any other non-JSON error is structured.
            var handler = new FakeHandler()
                .Respond(429, "Rate limit exceeded", "text/plain", ("Retry-After", "0"))
                .Respond(403, "Forbidden", "text/plain");
            using var client = Clients.Create(handler);

            var ex = await Assert.ThrowsAsync<AnvilClientException>(() => client.RequestRestBytesAsync(HttpMethod.Post, "/api/v1/fill/x.pdf", new { }));

            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("403: Forbidden", ex.Message);
            Assert.Equal("Forbidden", ex.ResponseContent);
#pragma warning disable CS0618
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, ex.HttpStatusCode);
            Assert.True(ex.ResponseHeaders!.ContainsKey("Content-Type"));
#pragma warning restore CS0618
        }

        [Fact]
        public async Task Dispose_DisposesAnOwnedHttpClientOnly()
        {
            var handler = new FakeHandler().RespondJson("{}");
            using var http = new HttpClient(handler);
            var client = Clients.Create(handler, o => o.HttpClient = http);

            client.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.RequestRestAsync(HttpMethod.Get, "/api/v1/x"));
            // Still usable: the client didn't dispose it
            using var response = await http.GetAsync("https://app.useanvil.com/api/v1/x");
            Assert.Equal(200, (int)response.StatusCode);
        }
    }
}
