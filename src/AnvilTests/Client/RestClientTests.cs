using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Xunit;

using Anvil.Client;
using Anvil.Payloads;

using Moq;

namespace AnvilTests.Client
{
    public class RestClientTests
    {
        private RestClient _client;

        public RestClientTests()
        {
            _client = new RestClient("a-test-key");
        }

        // [Fact]
        // public Task Client_Creates()
        // {
        //     var mockHttp = new Mock<HttpClient>();
        //     // mockHttp.Setup(x => x.SendAsync(new HttpRequestMessage()));
        //     // Create wrapper for HTTPClient and use it as 
        //
        //     var stream = _client.FillPDF("my-template-id",
        //         new FillPDF()
        //         {
        //             Title = "the title",
        //             Data = new Dictionary<string, dynamic>() {{"what", "value"}, {"what2", "value"},}
        //         }
        //     ).Result;
        // }
    }
}