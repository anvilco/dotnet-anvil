using System;
using System.Text;

using Anvil;

using Xunit;

namespace AnvilTests
{
    public class AuthTests
    {
        private static string Base64(string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value));

        private static string Base64Url(string value)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // A JWT-shaped token: a header with an alg, a JSON object payload, any signature
        private static readonly string Jwt = $"{Base64Url(@"{""alg"":""HS256"",""typ"":""JWT""}")}.{Base64Url(@"{""sub"":""user1""}")}.signature";

        [Fact]
        public void ApiKey_IsBasicWithATrailingColon()
        {
            Assert.Equal($"Basic {Base64("my-api-key:")}", Auth.Header("my-api-key", null));
        }

        [Fact]
        public void AccessToken_IsBearerBase64()
        {
            Assert.Equal($"Bearer {Base64("my-token")}", Auth.Header(null, "my-token"));
        }

        [Fact]
        public void AccessToken_WinsOverApiKey()
        {
            Assert.StartsWith("Bearer ", Auth.Header("my-api-key", "my-token"));
        }

        [Fact]
        public void AccessToken_ThatIsAJwt_IsSentAsIs()
        {
            Assert.Equal($"Bearer {Jwt}", Auth.Header(null, Jwt));
        }

        [Theory]
        [InlineData("a.b.c")]
        [InlineData("only.two")]
        [InlineData("a.b.c.d")]
        public void AccessToken_ThatOnlyLooksLikeAJwt_IsEncoded(string token)
        {
            Assert.Equal($"Bearer {Base64(token)}", Auth.Header(null, token));
        }

        [Fact]
        public void IsJwt_NeedsAStringAlgInTheHeader()
        {
            var noAlg = $"{Base64Url(@"{""typ"":""JWT""}")}.{Base64Url(@"{""sub"":""user1""}")}.sig";
            var numericAlg = $"{Base64Url(@"{""alg"":1}")}.{Base64Url(@"{""sub"":""user1""}")}.sig";

            Assert.True(Auth.IsJwt(Jwt));
            Assert.False(Auth.IsJwt(noAlg));
            Assert.False(Auth.IsJwt(numericAlg));
        }

        [Fact]
        public void IsJwt_NeedsJsonObjects()
        {
            var stringPayload = $"{Base64Url(@"{""alg"":""HS256""}")}.{Base64Url(@"""just a string""")}.sig";
            var notJson = $"{Base64Url(@"{""alg"":""HS256""}")}.{Base64Url("not json")}.sig";

            Assert.False(Auth.IsJwt(stringPayload));
            Assert.False(Auth.IsJwt(notJson));
        }

        [Fact]
        public void IsJwt_AllowsAnEmptySignature()
        {
            Assert.True(Auth.IsJwt(Jwt.Substring(0, Jwt.LastIndexOf('.') + 1)));
        }

        [Fact]
        public void NoCredentials_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => Auth.Header(null, null));
            Assert.Contains("apiKey or accessToken", ex.Message);
            Assert.Throws<ArgumentException>(() => Auth.Header("  ", ""));
        }

        [Fact]
        public void Client_WithNeitherCredential_ReadsTheEnvironment()
        {
            var previous = Environment.GetEnvironmentVariable("ANVIL_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("ANVIL_API_KEY", null);
                var ex = Assert.Throws<ArgumentException>(() => new AnvilClient(new AnvilClientOptions()));
                Assert.Contains("ANVIL_API_KEY", ex.Message);

                Environment.SetEnvironmentVariable("ANVIL_API_KEY", "from-env");
                using var client = new AnvilClient(new AnvilClientOptions());
                Assert.NotNull(client);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ANVIL_API_KEY", previous);
            }
        }
    }
}
