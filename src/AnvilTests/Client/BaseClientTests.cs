using Xunit;

using Anvil;

namespace AnvilTests.Client
{
    public class FakeClient : BaseClient
    {
        public string? ApiKey
        {
            get => _apiKey;
            set => _apiKey = value;
        }

        public new string EncodeApiKey()
        {
            return base.EncodeApiKey();
        }
    }

    public class BaseClientTests
    {
        private readonly FakeClient _client;

        public BaseClientTests()
        {
            _client = new FakeClient();
        }

        [Fact]
        public void EncodeAPIKey_IsNull()
        {
            Assert.Throws<AnvilClientException>(() => _client.EncodeApiKey());
        }

        [Fact]
        public void EncodeAPIKey_IsEmptyOrWhitespace()
        {
            _client.ApiKey = "";
            Assert.Throws<AnvilClientException>(() => _client.EncodeApiKey());

            _client.ApiKey = "    ";
            Assert.Throws<AnvilClientException>(() => _client.EncodeApiKey());

            _client.ApiKey = "\t\n   ";
            Assert.Throws<AnvilClientException>(() => _client.EncodeApiKey());
        }

        [Fact]
        public void EncodeAPIKey_Is()
        {
            _client.ApiKey = "my-api-key";
            const string expected = "bXktYXBpLWtleQ==";
            Assert.Equal(_client.EncodeApiKey(), expected);
        }
    }
}