using System;
using System.Collections.Generic;
using System.Net;

namespace Anvil
{
    public class AnvilClientException : Exception
    {
        public HttpStatusCode? HttpStatusCode { get; set; }
        public Dictionary<string, IEnumerable<string>>? ResponseHeaders { get; set; }
        public string? ResponseContent { get; set; }

        public AnvilClientException(string message) : base(message)
        {
        }
    }

    public abstract class BaseClient
    {
        protected string? _apiKey;

        protected string EncodeApiKey()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new AnvilClientException("API key cannot be null or empty");
            }

            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_apiKey));
        }
    }
}