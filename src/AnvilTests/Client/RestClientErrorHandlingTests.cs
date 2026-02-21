using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anvil;
using Anvil.Client;
using Xunit;

namespace AnvilTests.Client
{
    /// <summary>
    /// Integration tests for error handling scenarios in RestClient.
    /// These tests simulate real-world API error responses like rate limits and non-JSON errors.
    /// </summary>
    public class RestClientErrorHandlingTests
    {
        [Fact]
        public void CreateExceptionFromResponse_HandlesRateLimitError()
        {
            // Arrange: Create a 429 rate limit response with Retry-After header
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "5");
            response.Content = new StringContent("Rate limit exceeded", Encoding.UTF8, "text/plain");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.TooManyRequests, exception.HttpStatusCode);
            Assert.NotNull(exception.ResponseHeaders);
            Assert.True(exception.ResponseHeaders.ContainsKey("Retry-After"));
            Assert.Equal("5", exception.ResponseHeaders["Retry-After"].First());
            Assert.NotNull(exception.ResponseContent);
            Assert.Contains("Rate limit exceeded", exception.ResponseContent);
            Assert.Equal("Error: TooManyRequests", exception.Message);
        }

        [Fact]
        public void CreateExceptionFromResponse_HandlesJsonErrorResponse()
        {
            // Arrange: Create a 400 error with JSON error response
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var jsonError = @"{""errors"":[{""message"":""Invalid template ID""}]}";
            response.Content = new StringContent(jsonError, Encoding.UTF8, "application/json");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.BadRequest, exception.HttpStatusCode);
            Assert.NotNull(exception.ResponseContent);
            Assert.Equal(jsonError, exception.ResponseContent);
            Assert.Contains("Message1", exception.Data.Keys.Cast<string>());
            Assert.Equal("Invalid template ID", exception.Data["Message1"]);
        }

        [Fact]
        public void CreateExceptionFromResponse_HandlesNonJsonErrorResponse()
        {
            // Arrange: Create a 500 error with plain text response
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var plainTextError = "Internal Server Error - Service Unavailable";
            response.Content = new StringContent(plainTextError, Encoding.UTF8, "text/plain");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.InternalServerError, exception.HttpStatusCode);
            Assert.NotNull(exception.ResponseContent);
            Assert.Equal(plainTextError, exception.ResponseContent);
            Assert.Equal("Error: InternalServerError", exception.Message);
            // Since it's not JSON, the raw content should be in the Data dictionary
            Assert.Contains("RawContent", exception.Data.Keys.Cast<string>());
        }

        [Fact]
        public void CreateExceptionFromResponse_HandlesNotFoundError()
        {
            // Arrange: Create a 404 error
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            response.Content = new StringContent("Not Found", Encoding.UTF8, "text/plain");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.NotFound, exception.HttpStatusCode);
            Assert.Contains("Not Found", exception.Data.Keys.Cast<string>());
        }

        [Fact]
        public void CreateExceptionFromResponse_HandlesEmptyResponse()
        {
            // Arrange: Create an error with empty content
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
            response.Content = new StringContent("", Encoding.UTF8, "text/plain");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
            Assert.NotNull(exception.ResponseContent);
            Assert.Equal("", exception.ResponseContent);
        }

        [Fact]
        public void CreateExceptionFromResponse_CapturesResponseHeaders()
        {
            // Arrange: Create a response with response headers
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.Add("X-Request-Id", "12345");
            response.Content = new StringContent("Service temporarily unavailable", Encoding.UTF8, "text/html");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.NotNull(exception.ResponseHeaders);
            // HTTP headers are case-insensitive, so check with the normalized key
            var hasRequestId = exception.ResponseHeaders.ContainsKey("X-Request-Id") || exception.ResponseHeaders.ContainsKey("X-Request-ID");
            Assert.True(hasRequestId, "X-Request-Id header should be present");
            var requestIdValues = exception.ResponseHeaders.ContainsKey("X-Request-Id") 
                ? exception.ResponseHeaders["X-Request-Id"] 
                : exception.ResponseHeaders["X-Request-ID"];
            Assert.Equal("12345", requestIdValues.First());
        }

        [Fact]
        public void CreateExceptionFromResponse_HandlesMultipleErrorMessages()
        {
            // Arrange: Create a response with multiple error messages
            var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity);
            var jsonError = @"{""errors"":[{""message"":""Field 'name' is required""},{""message"":""Field 'email' is invalid""}]}";
            response.Content = new StringContent(jsonError, Encoding.UTF8, "application/json");

            // Act: Use reflection to call the private CreateExceptionFromResponse method
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert
            Assert.NotNull(exception);
            Assert.Contains("Message1", exception.Data.Keys.Cast<string>());
            Assert.Contains("Message2", exception.Data.Keys.Cast<string>());
            Assert.Equal("Field 'name' is required", exception.Data["Message1"]);
            Assert.Equal("Field 'email' is invalid", exception.Data["Message2"]);
        }

        [Fact]
        public void CreateExceptionFromResponse_JsonWithNoErrorsKey_DoesNotCrash()
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var jsonContent = @"{""error"":""something unexpected""}";
            response.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.BadRequest, exception.HttpStatusCode);
            Assert.Equal(jsonContent, exception.ResponseContent);
            Assert.Empty(exception.Data);
        }

        [Fact]
        public void CreateExceptionFromResponse_IncludesContentHeaders()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            response.Content = new StringContent("error", Encoding.UTF8, "text/plain");

            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            Assert.NotNull(exception);
            Assert.NotNull(exception.ResponseHeaders);
            Assert.True(exception.ResponseHeaders.ContainsKey("Content-Type"));
        }

        [Fact]
        public void CreateExceptionFromResponse_NotFoundWithJsonErrors_DoesNotDuplicateKey()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            var jsonContent = @"{""errors"":[{""message"":""Resource not found""}]}";
            response.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            Assert.NotNull(exception);
            Assert.Equal(HttpStatusCode.NotFound, exception.HttpStatusCode);
            Assert.Equal("Resource not found", exception.Data["Message1"]);
            Assert.Contains("Not Found", exception.Data.Keys.Cast<string>());
        }
    }
}
