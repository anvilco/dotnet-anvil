using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Anvil
{
    /// <summary>
    /// Turns a GraphQL operation whose variables hold <see cref="GraphQLUpload"/>s
    /// into a multipart request, per the GraphQL multipart request spec:
    /// <c>operations</c> (the operation with each file replaced by <c>null</c>),
    /// <c>map</c> (part name to the paths of the files) and one part per file.
    /// </summary>
    internal static class Uploads
    {
        private static readonly Regex IndexPath = new Regex(@"\[(\d+)\]", RegexOptions.Compiled);

        /// <summary>The uploads in the operation's variables, by the paths they sit at, in order of first appearance.</summary>
        public static List<KeyValuePair<GraphQLUpload, List<string>>> Extract(JObject operation)
        {
            var found = new List<KeyValuePair<GraphQLUpload, List<string>>>();
            var variables = operation["variables"];
            if (variables == null)
            {
                return found;
            }

            foreach (var value in All(variables).OfType<JValue>().Where(v => v.Type == JTokenType.String).ToList())
            {
                var upload = GraphQLUpload.FromSentinel((string)value.Value!);
                if (upload == null)
                {
                    continue;
                }

                // JToken paths are `variables.files[0].file`; the spec's are `variables.files.0.file`
                var path = IndexPath.Replace(value.Path, ".$1");
                var entry = found.FirstOrDefault(e => ReferenceEquals(e.Key, upload));
                if (entry.Key == null)
                {
                    found.Add(new KeyValuePair<GraphQLUpload, List<string>>(upload, new List<string> { path }));
                }
                else
                {
                    entry.Value.Add(path);
                }

                value.Replace(JValue.CreateNull());
            }

            return found;
        }

        /// <summary>
        /// Checks the variables the way node-anvil does: a file may only sit under a
        /// <c>file</c> key, and what sits under a <c>file</c> key must be an upload,
        /// a base64 object with <c>data</c>, <c>filename</c> and <c>mimetype</c>, or a
        /// <c>url</c> object. Throws <see cref="ArgumentException"/> otherwise.
        /// </summary>
        public static void Validate(JObject operation)
        {
            var variables = operation["variables"];
            if (variables == null)
            {
                return;
            }

            foreach (var property in All(variables).OfType<JProperty>())
            {
                var value = property.Value;
                var isUpload = value.Type == JTokenType.String && GraphQLUpload.FromSentinel((string)value!) != null;
                if (property.Name != "file")
                {
                    if (isUpload)
                    {
                        throw new ArgumentException($"Invalid File schema detected: a file must be the value of a `file` key, not `{property.Name}` (at {property.Path})");
                    }

                    continue;
                }

                switch (value.Type)
                {
                    case JTokenType.Null:
                        break;
                    case JTokenType.String when isUpload:
                        break;
                    case JTokenType.String:
                        throw new ArgumentException($"Invalid File schema detected: `file` at {property.Path} is a string. Pass a file with AnvilClient.PrepareGraphQLFile, or a base64 object with data, filename and mimetype.");
                    case JTokenType.Object:
                        var obj = (JObject)value;
                        if (obj["data"] != null)
                        {
                            if (obj["data"]!.Type != JTokenType.String || IsBlank(obj["filename"]) || IsBlank(obj["mimetype"]))
                            {
                                throw new ArgumentException($"Invalid File schema detected: a base64 file at {property.Path} needs string data, filename and mimetype");
                            }
                        }
                        else if (IsBlank(obj["url"]))
                        {
                            throw new ArgumentException($"Invalid File schema detected: `file` at {property.Path} must be a file from AnvilClient.PrepareGraphQLFile, a base64 object (data, filename, mimetype) or a url object");
                        }

                        break;
                    default:
                        throw new ArgumentException($"Invalid File schema detected: `file` at {property.Path} must be a file from AnvilClient.PrepareGraphQLFile, a base64 object (data, filename, mimetype) or a url object");
                }
            }
        }

        private static IEnumerable<JToken> All(JToken token) => token is JContainer container ? container.DescendantsAndSelf() : new[] { token };

        private static bool IsBlank(JToken? token) => token == null || token.Type != JTokenType.String || string.IsNullOrEmpty((string?)token);

        /// <summary>
        /// The multipart body for one attempt. The file parts are opened fresh each
        /// time, so the request can be sent again after a 429.
        /// </summary>
        public static MultipartFormDataContent Build(JObject operation, List<KeyValuePair<GraphQLUpload, List<string>>> uploads)
        {
            var body = new MultipartFormDataContent();
            body.Add(Part(new StringContent(operation.ToString(Formatting.None), Encoding.UTF8, "application/json"), "operations"));

            var map = new JObject();
            var number = 1;
            foreach (var entry in uploads)
            {
                map[number.ToString()] = new JArray(entry.Value);
                number++;
            }

            body.Add(Part(new StringContent(map.ToString(Formatting.None), Encoding.UTF8, "application/json"), "map"));

            number = 1;
            foreach (var entry in uploads)
            {
                body.Add(Part(entry.Key.OpenContent(), number.ToString(), entry.Key.Filename));
                number++;
            }

            return body;
        }

        // Quoted, as the multipart spec's servers expect; .NET leaves simple tokens bare
        private static HttpContent Part(HttpContent content, string name, string? filename = null)
        {
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = $"\"{name}\"",
                FileName = filename == null ? null : $"\"{filename.Replace("\"", "")}\"",
            };
            return content;
        }
    }
}
