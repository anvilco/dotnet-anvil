using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Anvil;
using Anvil.Client;
using GraphQL.Client.Http;
using Xunit;

namespace AnvilTests.Client
{
    public class GraphQLClientErrorHandlingTests
    {
        private static HttpResponseHeaders CreateResponseHeaders(Action<HttpResponseHeaders>? configure = null)
        {
            var response = new HttpResponseMessage();
            configure?.Invoke(response.Headers);
            return response.Headers;
        }

        [Fact]
        public void WrapGraphQLException_JsonErrorResponse_WrapsInAnvilClientException()
        {
            var jsonContent = @"{""errors"":[{""message"":""Invalid query""}]}";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.BadRequest, headers, jsonContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.IsType<AnvilClientException>(result);
            Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
            Assert.Equal(jsonContent, result.ResponseContent);
            Assert.Equal("Invalid query", result.Message);
        }

        [Fact]
        public void WrapGraphQLException_NonJsonResponse_DoesNotCrash()
        {
            var plainContent = "Rate limit exceeded - try again later";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.TooManyRequests, headers, plainContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.IsType<AnvilClientException>(result);
            Assert.Equal(HttpStatusCode.TooManyRequests, result.HttpStatusCode);
            Assert.Equal(plainContent, result.ResponseContent);
            Assert.Equal("Error: TooManyRequests", result.Message);
        }

        [Fact]
        public void WrapGraphQLException_Unauthorized_WrapsInAnvilClientException()
        {
            var jsonContent = @"{""errors"":[{""message"":""Unauthorized""}]}";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.Unauthorized, headers, jsonContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.IsType<AnvilClientException>(result);
            Assert.Equal(HttpStatusCode.Unauthorized, result.HttpStatusCode);
            Assert.Equal("Unauthorized", result.Message);
        }

        [Fact]
        public void WrapGraphQLException_PreservesInnerException()
        {
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.InternalServerError, headers, "error");

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.NotNull(result.InnerException);
            Assert.IsType<GraphQLHttpRequestException>(result.InnerException);
            Assert.Same(ex, result.InnerException);
        }

        [Fact]
        public void WrapGraphQLException_CopiesResponseHeaders()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            responseMessage.Headers.Add("X-Request-Id", "abc-123");
            responseMessage.Headers.Add("X-RateLimit-Remaining", "0");
            var ex = new GraphQLHttpRequestException(HttpStatusCode.TooManyRequests, responseMessage.Headers, "error");

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.NotNull(result.ResponseHeaders);
            Assert.True(result.ResponseHeaders.ContainsKey("X-Request-Id"));
            Assert.Equal("abc-123", result.ResponseHeaders["X-Request-Id"].First());
            Assert.True(result.ResponseHeaders.ContainsKey("X-RateLimit-Remaining"));
            Assert.Equal("0", result.ResponseHeaders["X-RateLimit-Remaining"].First());
        }

        [Fact]
        public void WrapGraphQLException_NullContent_DoesNotCrash()
        {
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.InternalServerError, headers, null);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.IsType<AnvilClientException>(result);
            Assert.Equal(HttpStatusCode.InternalServerError, result.HttpStatusCode);
            Assert.Null(result.ResponseContent);
            Assert.Equal("Error: InternalServerError", result.Message);
        }

        [Fact]
        public void WrapGraphQLException_MultipleErrors_JoinedWithSemicolon()
        {
            var jsonContent = @"{""errors"":[{""message"":""Field 'name' is required""},{""message"":""Field 'email' is invalid""}]}";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.BadRequest, headers, jsonContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.Equal("Field 'name' is required; Field 'email' is invalid", result.Message);
            Assert.Contains("Message1", result.Data.Keys.Cast<string>());
            Assert.Contains("Message2", result.Data.Keys.Cast<string>());
            Assert.Equal("Field 'name' is required", result.Data["Message1"]);
            Assert.Equal("Field 'email' is invalid", result.Data["Message2"]);
        }

        [Fact]
        public void WrapGraphQLException_JsonWithNoErrorsKey_FallsBackToStatusCode()
        {
            var jsonContent = @"{""error"":""something went wrong""}";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.BadRequest, headers, jsonContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.Equal("Error: BadRequest", result.Message);
            Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
            Assert.Equal(jsonContent, result.ResponseContent);
            Assert.Empty(result.Data);
        }

        [Fact]
        public void WrapGraphQLException_ErrorsWithNullMessages_SkipsNulls()
        {
            var jsonContent = @"{""errors"":[{},{""message"":""real error""},{""other"":""field""}]}";
            var headers = CreateResponseHeaders();
            var ex = new GraphQLHttpRequestException(HttpStatusCode.BadRequest, headers, jsonContent);

            var result = GraphQLClient.WrapGraphQLException(ex);

            Assert.Equal("real error", result.Message);
            Assert.Equal(1, result.Data.Count);
            Assert.Equal("real error", result.Data["Message1"]);
        }
    }
}