using System;

namespace Anvil
{
    public class AnvilClientException : Exception
    {
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