using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Anvil;

namespace AnvilTests.Support
{
    /// <summary>A request the <see cref="FakeHandler"/> received.</summary>
    public sealed class RecordedRequest
    {
        public RecordedRequest(HttpRequestMessage request, string? body)
        {
            Method = request.Method;
            Uri = request.RequestUri!;
            Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            ContentType = request.Content?.Headers.ContentType?.ToString();
            Body = body;
        }

        public HttpMethod Method { get; }
        public Uri Uri { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public string? ContentType { get; }
        public string? Body { get; }

        public string Path => Uri.AbsolutePath;
        public string PathAndQuery => Uri.PathAndQuery;
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that answers with scripted responses, in
    /// order, and records what it was sent. The last response repeats once the
    /// script runs out.
    /// </summary>
    public sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        private Func<HttpRequestMessage, HttpResponseMessage>? _last;

        public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

        public RecordedRequest LastRequest => Requests[Requests.Count - 1];

        public FakeHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responses.Enqueue(responder);
            return this;
        }

        public FakeHandler Respond(int status, string body, string contentType = "application/json", params (string Name, string Value)[] headers)
        {
            return Respond(_ => Response(status, body, contentType, headers));
        }

        public FakeHandler RespondJson(string json, params (string Name, string Value)[] headers) => Respond(200, json, "application/json", headers);

        public FakeHandler RespondPdf(byte[]? bytes = null, params (string Name, string Value)[] headers)
        {
            return Respond(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes ?? Pdf) };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                AddHeaders(response, headers);
                return response;
            });
        }

        public FakeHandler Throw(Exception exception) => Respond(_ => throw exception);

        /// <summary>Answers after a delay, honoring cancellation like a real connection would.</summary>
        public FakeHandler RespondAfter(int milliseconds, string body = "{}")
        {
            return Respond(_ => new DelayedResponse(milliseconds, body));
        }

        private sealed class DelayedResponse : HttpResponseMessage
        {
            public DelayedResponse(int milliseconds, string body)
            {
                Milliseconds = milliseconds;
                Body = body;
            }

            public int Milliseconds { get; }
            public string Body { get; }
        }

        public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 fake");

        public static HttpResponseMessage Response(int status, string body, string contentType = "application/json", params (string Name, string Value)[] headers)
        {
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            };
            AddHeaders(response, headers);
            return response;
        }

        private static void AddHeaders(HttpResponseMessage response, (string Name, string Value)[] headers)
        {
            foreach (var header in headers)
            {
                response.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            lock (Requests)
            {
                Requests.Add(new RecordedRequest(request, body));
            }

            Func<HttpRequestMessage, HttpResponseMessage> responder;
            lock (_responses)
            {
                if (_responses.Count > 0)
                {
                    _last = _responses.Dequeue();
                }

                responder = _last ?? throw new InvalidOperationException("No response scripted");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var response = responder(request);
            if (response is DelayedResponse delayed)
            {
                await Task.Delay(delayed.Milliseconds, cancellationToken);
                return Response(200, delayed.Body);
            }

            return response;
        }
    }

    public static class Clients
    {
        /// <summary>
        /// A client sending through <paramref name="handler"/>. The API key is unique
        /// per client, since clients with the same key share a rate limiter.
        /// </summary>
        public static AnvilClient Create(FakeHandler handler, Action<AnvilClientOptions>? configure = null)
        {
            var options = new AnvilClientOptions
            {
                ApiKey = "key-" + Guid.NewGuid().ToString("N"),
                HttpMessageHandler = handler,
                MaxRetries = 0,
            };
            configure?.Invoke(options);
            return new AnvilClient(options);
        }
    }
}
