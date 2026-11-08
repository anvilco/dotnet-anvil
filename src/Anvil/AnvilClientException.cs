using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

using Newtonsoft.Json.Linq;

namespace Anvil
{
    /// <summary>A field-level detail of a validation error.</summary>
    public sealed class AnvilErrorField
    {
        public AnvilErrorField(string? property, string? message)
        {
            Property = property;
            Message = message;
        }

        /// <summary>The field the message is about, e.g. <c>data</c>.</summary>
        public string? Property { get; }

        public string? Message { get; }

        public override string ToString() => Property != null ? $"{Property}: {Message}" : Message ?? "";
    }

    /// <summary>
    /// One error from the API, normalized from the GraphQL and REST error shapes.
    /// </summary>
    public sealed class AnvilError
    {
        public AnvilError(string? name, string? message, IReadOnlyList<AnvilErrorField>? fields = null, JToken? raw = null)
        {
            Name = name;
            Message = message;
            Fields = fields ?? Array.Empty<AnvilErrorField>();
            Raw = raw;
        }

        /// <summary>The error's name, e.g. <c>ValidationError</c>, <c>AuthorizationError</c> or <c>Not Found</c>.</summary>
        public string? Name { get; }

        public string? Message { get; }

        /// <summary>Field-level details of a validation error.</summary>
        public IReadOnlyList<AnvilErrorField> Fields { get; }

        /// <summary>The error as the API sent it, with any members this type has no property for, like a GraphQL error's <c>path</c>.</summary>
        public JToken? Raw { get; }

        /// <summary><c>name: message (property: message, ...)</c>.</summary>
        public override string ToString()
        {
            var text = Name != null && Message != null && Name != Message
                ? $"{Name}: {Message}"
                : Message ?? Name ?? "Unknown Error";
            if (Fields.Count > 0)
            {
                text += $" ({string.Join(", ", Fields.Select(f => f.ToString()))})";
            }

            return text;
        }
    }

    /// <summary>
    /// The API answered with an error: a status outside 2xx, a GraphQL response
    /// with <c>errors</c>, or a response that isn't the JSON it claims to be.
    /// <see cref="Errors"/> holds the normalized errors; <see cref="Exception.Message"/> is
    /// <c>"{status}: {name}: {message} (field: message, ...)"</c>.
    /// </summary>
    public class AnvilClientException : Exception
    {
        private static readonly IReadOnlyDictionary<string, IEnumerable<string>> NoHeaders
            = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public AnvilClientException(string message) : this(message, (Exception?)null)
        {
        }

        public AnvilClientException(string message, Exception? innerException) : base(message, innerException)
        {
            Errors = new[] { new AnvilError(null, message) };
            Headers = NoHeaders;
        }

        public AnvilClientException(
            IReadOnlyList<AnvilError> errors,
            int statusCode,
            IReadOnlyDictionary<string, IEnumerable<string>>? headers = null,
            string? responseContent = null,
            JToken? responseData = null,
            HttpResponseMessage? response = null,
            Exception? innerException = null)
            : base(FormatMessage(errors, statusCode), innerException)
        {
            Errors = errors;
            StatusCode = statusCode;
            Headers = headers ?? NoHeaders;
            ResponseContent = responseContent;
            ResponseData = responseData;
            Response = response;

            // Each message is also in Data as Message1, Message2, ..., as before 1.0
            var count = 1;
            foreach (var error in errors)
            {
                if (error.Message != null)
                {
                    Data[$"Message{count}"] = error.Message;
                    count++;
                }
            }
        }

        /// <summary>The normalized errors. At least one.</summary>
        public IReadOnlyList<AnvilError> Errors { get; }

        /// <summary>The HTTP status, or 0 when there was no response.</summary>
        public int StatusCode { get; }

        /// <summary>The response's headers, including the content headers. Empty when there was no response.</summary>
        public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }

        /// <summary>The response body, or the start of it for a large non-JSON body.</summary>
        public string? ResponseContent { get; }

        /// <summary>Any <c>data</c> the GraphQL response returned alongside its errors. A GraphQL request can partly succeed.</summary>
        public JToken? ResponseData { get; }

        /// <summary>The response, with its body already read.</summary>
        public HttpResponseMessage? Response { get; }

        [Obsolete("Use StatusCode, which is 0 when there was no response.")]
        public HttpStatusCode? HttpStatusCode => StatusCode == 0 ? (HttpStatusCode?)null : (HttpStatusCode)StatusCode;

        [Obsolete("Use Headers.")]
        public Dictionary<string, IEnumerable<string>>? ResponseHeaders
            => Headers.Count == 0 ? null : new Dictionary<string, IEnumerable<string>>(Headers.ToDictionary(h => h.Key, h => h.Value), StringComparer.OrdinalIgnoreCase);

        private static string FormatMessage(IReadOnlyList<AnvilError> errors, int statusCode)
        {
            var description = errors.Count > 0
                ? string.Join("; ", errors.Select(e => e.ToString()))
                : "Unknown Error";
            return statusCode > 0 ? $"{statusCode}: {description}" : description;
        }
    }

    /// <summary>
    /// The base of the pre-1.0 <see cref="Client.RestClient"/> and <see cref="Client.GraphQLClient"/>.
    /// </summary>
    [Obsolete("Use AnvilClient.")]
    public abstract class BaseClient
    {
        protected string? _apiKey;

        /// <summary>The API key, base64 encoded with its trailing colon, as the Basic auth header carries it.</summary>
        protected string EncodeApiKey()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new AnvilClientException("API key cannot be null or empty");
            }

            return Auth.Header(_apiKey, null).Substring("Basic ".Length);
        }
    }
}
