using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anvil;
using Xunit;

namespace AnvilTests.Client
{
    public class ErrorHandlingTests
    {
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public TestHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public void AnvilClientException_HasHttpStatusCodeProperty()
        {
            var exception = new AnvilClientException("Test error")
            {
                HttpStatusCode = HttpStatusCode.TooManyRequests
            };

            Assert.Equal(HttpStatusCode.TooManyRequests, exception.HttpStatusCode);
        }

        [Fact]
        public void AnvilClientException_HasResponseHeadersProperty()
        {
            var exception = new AnvilClientException("Test error")
            {
                ResponseHeaders = new Dictionary<string, IEnumerable<string>>
                {
                    { "Retry-After", new[] { "5" } }
                }
            };

            Assert.NotNull(exception.ResponseHeaders);
            Assert.True(exception.ResponseHeaders.ContainsKey("Retry-After"));
            Assert.Equal("5", exception.ResponseHeaders["Retry-After"].First());
        }

        [Fact]
        public void AnvilClientException_HasResponseContentProperty()
        {
            var exception = new AnvilClientException("Test error")
            {
                ResponseContent = "Rate limit exceeded"
            };

            Assert.Equal("Rate limit exceeded", exception.ResponseContent);
        }

        [Fact]
        public void AnvilClientException_CanHaveAllPropertiesSet()
        {
            var exception = new AnvilClientException("Test error")
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                ResponseHeaders = new Dictionary<string, IEnumerable<string>>
                {
                    { "X-Custom-Header", new[] { "custom-value" } }
                },
                ResponseContent = "{\"error\": \"bad request\"}"
            };

            Assert.Equal(HttpStatusCode.BadRequest, exception.HttpStatusCode);
            Assert.NotNull(exception.ResponseHeaders);
            Assert.Equal("{\"error\": \"bad request\"}", exception.ResponseContent);
        }
    }
}