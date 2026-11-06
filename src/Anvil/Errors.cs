using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Anvil
{
    /// <summary>Turns the API's error shapes into <see cref="AnvilError"/>s, like node-anvil's errors.js.</summary>
    internal static class Errors
    {
        // How much of a non-JSON body (a proxy's HTML, say) to keep
        private const int BodyPreviewLength = 2000;

        /// <summary>Whether a JSON body is an error: GraphQL <c>errors</c>, or a REST <c>{name, message}</c>.</summary>
        public static bool LooksLikeJsonError(JToken? json)
        {
            if (!(json is JObject obj))
            {
                return false;
            }

            return (obj["errors"] is JArray errors && errors.Count > 0)
                || obj["message"] != null
                || obj["name"] != null;
        }

        /// <summary>
        /// The errors in a JSON body: a GraphQL <c>errors</c> array as-is, a REST
        /// <c>{name, message, fields}</c> as one error, else one error from the
        /// status text.
        /// </summary>
        public static IReadOnlyList<AnvilError> NormalizeJson(JToken? json, string? reasonPhrase)
        {
            if (json is JObject obj)
            {
                if (obj["errors"] is JArray errors && errors.Count > 0)
                {
                    return errors.Select(FromToken).ToList();
                }

                if (obj["message"] != null || obj["name"] != null)
                {
                    return new[] { FromToken(obj) };
                }
            }

            return FromStatus(reasonPhrase, null);
        }

        /// <summary>A body that isn't JSON, like a 502 from a proxy: the status text, with the start of the body.</summary>
        public static IReadOnlyList<AnvilError> FromStatus(string? reasonPhrase, string? body)
        {
            var text = string.IsNullOrEmpty(reasonPhrase) ? "Unknown Error" : reasonPhrase!;
            var raw = new JObject { ["name"] = text, ["message"] = text };
            if (!string.IsNullOrEmpty(body))
            {
                raw["body"] = body!.Length > BodyPreviewLength ? body.Substring(0, BodyPreviewLength) : body;
            }

            return new[] { new AnvilError(text, text, null, raw) };
        }

        /// <summary>A response that claims to be JSON but can't be parsed, or was cut short.</summary>
        public static IReadOnlyList<AnvilError> FromException(Exception exception)
        {
            var raw = new JObject { ["name"] = exception.GetType().Name, ["message"] = exception.Message };
            return new[] { new AnvilError(exception.GetType().Name, exception.Message, null, raw) };
        }

        private static AnvilError FromToken(JToken token)
        {
            if (!(token is JObject obj))
            {
                return new AnvilError(null, token.ToString(), null, token);
            }

            var fields = (obj["fields"] as JArray)?
                .OfType<JObject>()
                .Select(f => new AnvilErrorField(f["property"]?.ToString(), f["message"]?.ToString()))
                .ToList();
            return new AnvilError(obj["name"]?.ToString(), obj["message"]?.ToString(), fields, obj);
        }

        /// <summary>The response's headers and content headers, case-insensitive.</summary>
        public static IReadOnlyDictionary<string, IEnumerable<string>> Headers(HttpResponseMessage response)
        {
            var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = header.Value.ToList();
            }

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    headers[header.Key] = header.Value.ToList();
                }
            }

            return headers;
        }
    }
}
