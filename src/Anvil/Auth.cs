using System;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace Anvil
{
    /// <summary>Builds the <c>Authorization</c> header the way node-anvil does.</summary>
    internal static class Auth
    {
        // Three base64url segments; the signature may be empty
        private static readonly Regex JwtPattern = new Regex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$", RegexOptions.Compiled);

        /// <summary>
        /// The <c>Authorization</c> header value. An access token wins over an API
        /// key. An API key is <c>Basic base64(apiKey + ":")</c>. A JWT access token
        /// is sent as-is, any other access token base64 encoded.
        /// </summary>
        public static string Header(string? apiKey, string? accessToken)
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return IsJwt(accessToken!) ? $"Bearer {accessToken}" : $"Bearer {Base64(accessToken!)}";
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return $"Basic {Base64(apiKey + ":")}";
            }

            throw new ArgumentException("apiKey or accessToken is required, or set the ANVIL_API_KEY environment variable");
        }

        /// <summary>
        /// Whether the token is a JWT: three base64url segments, whose header and
        /// payload decode to JSON objects, with a string <c>alg</c> in the header.
        /// </summary>
        public static bool IsJwt(string token)
        {
            if (!JwtPattern.IsMatch(token))
            {
                return false;
            }

            var segments = token.Split('.');
            var header = DecodeJsonObject(segments[0]);
            var payload = DecodeJsonObject(segments[1]);
            return header != null && payload != null && header["alg"]?.Type == JTokenType.String;
        }

        private static JObject? DecodeJsonObject(string base64Url)
        {
            try
            {
                var base64 = base64Url.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                    case 1: return null;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                return JToken.Parse(json) as JObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Base64(string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value));
    }
}
