using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Anvil;
using Anvil.Client;
using Xunit;

namespace AnvilTests.Client
{
    /// <summary>
    /// Tests that validate the fix for the original issue: 
    /// "Client crashes on api error responses and throws JSON parsing error"
    /// </summary>
    public class IssueRegressionTests
    {
        [Fact]
        public void Issue_RateLimitError_DoesNotThrowJsonParsingError()
        {
            // This test validates the fix for the issue where a 429 rate limit response
            // with non-JSON content would throw a JSON parsing error instead of 
            // properly handling the error.
            
            // Arrange: Create a 429 rate limit response like the one described in the issue
            // "429 Retry-After: 5"
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "5");
            // The actual response body might be empty or contain a simple text message
            response.Content = new StringContent("", Encoding.UTF8, "text/plain");

            // Act: Call CreateExceptionFromResponse - this should NOT throw a JSON parsing error
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            
            Exception exception = null;
            try
            {
                exception = (Exception)method.Invoke(client, new object[] { response });
            }
            catch (Exception ex)
            {
                // If we get here, the method threw an exception (like JsonException)
                Assert.True(false, $"CreateExceptionFromResponse threw an exception: {ex.InnerException?.GetType().Name} - {ex.InnerException?.Message}");
            }

            // Assert: The exception should be created successfully
            Assert.NotNull(exception);
            Assert.IsType<AnvilClientException>(exception);
            
            var anvilException = (AnvilClientException)exception;
            
            // Verify the exception contains the useful information mentioned in the suggested fix
            Assert.Equal(HttpStatusCode.TooManyRequests, anvilException.HttpStatusCode);
            Assert.NotNull(anvilException.ResponseHeaders);
            
            // Verify Retry-After header is accessible (case-insensitive check due to HTTP header normalization)
            var hasRetryAfter = anvilException.ResponseHeaders.ContainsKey("Retry-After") 
                || anvilException.ResponseHeaders.ContainsKey("Retry-after");
            Assert.True(hasRetryAfter, "Retry-After header should be accessible");
            
            // Verify the raw content is accessible
            Assert.NotNull(anvilException.ResponseContent);
        }

        [Fact]
        public void Issue_BeforeFix_JsonParsingWouldHaveFailed()
        {
            // This test demonstrates what the old code would have tried to do:
            // It would have tried to deserialize non-JSON content and crashed.
            
            // Arrange: Non-JSON content like what a 429 rate limit might return
            var nonJsonContent = "Rate limit exceeded";
            
            // Act & Assert: Trying to deserialize this as JSON should fail
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() =>
            {
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(nonJsonContent);
            });
        }

        [Fact]
        public void Issue_AfterFix_ExceptionContainsAllUsefulProperties()
        {
            // This test validates that the AnvilClientException now contains
            // all the useful properties mentioned in the suggested fix:
            // - HttpStatusCode
            // - ResponseHeaders
            // - ResponseContent
            
            // Arrange: Create a response with various error scenarios
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", "5");
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Content = new StringContent("Rate limit exceeded", Encoding.UTF8, "text/plain");

            // Act: Create exception from response
            var client = new RestClient("test-api-key");
            var method = typeof(RestClient).GetMethod("CreateExceptionFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            var exception = (AnvilClientException)method.Invoke(client, new object[] { response });

            // Assert: All suggested properties are present and accessible
            Assert.NotNull(exception.HttpStatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, exception.HttpStatusCode.Value);
            
            Assert.NotNull(exception.ResponseHeaders);
            Assert.True(exception.ResponseHeaders.Count > 0);
            
            Assert.NotNull(exception.ResponseContent);
            Assert.Equal("Rate limit exceeded", exception.ResponseContent);
            
            // Consumers can now access rate limit information from the exception
            Assert.Contains("Retry-After", exception.ResponseHeaders.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("X-RateLimit-Remaining", exception.ResponseHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }
}
