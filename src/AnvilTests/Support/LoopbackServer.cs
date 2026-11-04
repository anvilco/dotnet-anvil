using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AnvilTests.Support
{
    /// <summary>A request the <see cref="LoopbackServer"/> received.</summary>
    public sealed class ServerRequest
    {
        public ServerRequest(HttpListenerRequest request, byte[] body)
        {
            Method = request.HttpMethod;
            PathAndQuery = request.Url!.PathAndQuery;
            Headers = request.Headers.AllKeys.Where(k => k != null).ToDictionary(k => k!, k => request.Headers[k]!, StringComparer.OrdinalIgnoreCase);
            ContentType = request.ContentType;
            Body = body;
        }

        public string Method { get; }
        public string PathAndQuery { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public string? ContentType { get; }
        public byte[] Body { get; }
        public string BodyText => Encoding.UTF8.GetString(Body);
    }

    /// <summary>What the <see cref="LoopbackServer"/> answers with.</summary>
    public sealed class ServerResponse
    {
        public int Status { get; set; } = 200;
        public string? ContentType { get; set; } = "application/json";
        public byte[] Body { get; set; } = Array.Empty<byte>();
        public List<(string Name, string Value)> Headers { get; } = new List<(string, string)>();
        public int DelayMs { get; set; }

        public static ServerResponse Json(string json, params (string Name, string Value)[] headers)
        {
            var response = new ServerResponse { Body = Encoding.UTF8.GetBytes(json) };
            response.Headers.AddRange(headers);
            return response;
        }
    }

    /// <summary>
    /// A real HTTP server on a loopback port, answering with scripted responses,
    /// so the client's requests are checked on the wire: headers, bodies,
    /// multipart parts, rate limit timing, connection handling. The last response
    /// repeats once the script runs out.
    /// </summary>
    public sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Queue<Func<ServerRequest, ServerResponse>> _responses = new Queue<Func<ServerRequest, ServerResponse>>();
        private Func<ServerRequest, ServerResponse>? _last;
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();

        private LoopbackServer(HttpListener listener, string url)
        {
            _listener = listener;
            Url = url;
            Task.Run(ServeAsync);
        }

        /// <summary>The server's base URL, e.g. <c>http://127.0.0.1:51234</c>.</summary>
        public string Url { get; }

        public List<ServerRequest> Requests { get; } = new List<ServerRequest>();

        public ServerRequest LastRequest => Requests[Requests.Count - 1];

        public static LoopbackServer Start()
        {
            var random = new Random();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var port = random.Next(20000, 60000);
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                try
                {
                    listener.Start();
                    return new LoopbackServer(listener, $"http://127.0.0.1:{port}");
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                }
            }

            throw new InvalidOperationException("No free loopback port found");
        }

        /// <summary>A port nothing listens on, for connection failures.</summary>
        public static string ClosedUrl()
        {
            using var server = Start();
            return server.Url;
        }

        public LoopbackServer Respond(Func<ServerRequest, ServerResponse> responder)
        {
            lock (_responses)
            {
                _responses.Enqueue(responder);
            }

            return this;
        }

        public LoopbackServer Respond(int status, string body, string contentType = "application/json", params (string Name, string Value)[] headers)
        {
            return Respond(_ =>
            {
                var response = new ServerResponse { Status = status, ContentType = contentType, Body = Encoding.UTF8.GetBytes(body) };
                response.Headers.AddRange(headers);
                return response;
            });
        }

        public LoopbackServer RespondJson(string json, params (string Name, string Value)[] headers) => Respond(200, json, "application/json", headers);

        public LoopbackServer RespondBytes(byte[] bytes, string contentType = "application/pdf", params (string Name, string Value)[] headers)
        {
            return Respond(_ =>
            {
                var response = new ServerResponse { ContentType = contentType, Body = bytes };
                response.Headers.AddRange(headers);
                return response;
            });
        }

        public LoopbackServer RespondAfter(int milliseconds, string json = "{}")
        {
            return Respond(_ => new ServerResponse { Body = Encoding.UTF8.GetBytes(json), DelayMs = milliseconds });
        }

        private async Task ServeAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (_stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    continue;
                }

                _ = Task.Run(() => HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                byte[] body;
                using (var buffer = new MemoryStream())
                {
                    await context.Request.InputStream.CopyToAsync(buffer);
                    body = buffer.ToArray();
                }

                var request = new ServerRequest(context.Request, body);
                lock (Requests)
                {
                    Requests.Add(request);
                }

                Func<ServerRequest, ServerResponse> responder;
                lock (_responses)
                {
                    if (_responses.Count > 0)
                    {
                        _last = _responses.Dequeue();
                    }

                    responder = _last ?? (_ => ServerResponse.Json("{}"));
                }

                var response = responder(request);
                if (response.DelayMs > 0)
                {
                    await Task.Delay(response.DelayMs, _stopping.Token);
                }

                context.Response.StatusCode = response.Status;
                context.Response.ContentType = response.ContentType;
                foreach (var header in response.Headers)
                {
                    context.Response.Headers[header.Name] = header.Value;
                }

                context.Response.ContentLength64 = response.Body.Length;
                await context.Response.OutputStream.WriteAsync(response.Body, 0, response.Body.Length);
                context.Response.Close();
            }
            catch (Exception)
            {
                try
                {
                    context.Response.Abort();
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Close();
        }
    }

    /// <summary>A part of a multipart body.</summary>
    public sealed class MultipartPart
    {
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Body { get; set; } = Array.Empty<byte>();
        public string Text => Encoding.UTF8.GetString(Body);
        public string Name => Regex.Match(Headers["Content-Disposition"], "name=\"([^\"]*)\"").Groups[1].Value;
        public string? Filename => Headers["Content-Disposition"].Contains("filename=") ? Regex.Match(Headers["Content-Disposition"], "filename=\"([^\"]*)\"").Groups[1].Value : null;
    }

    public static class Multipart
    {
        /// <summary>Parses a multipart/form-data body into its parts.</summary>
        public static List<MultipartPart> Parse(string contentType, byte[] body)
        {
            var boundary = "--" + Regex.Match(contentType, "boundary=\"?([^\";]+)\"?").Groups[1].Value;
            var boundaryBytes = Encoding.ASCII.GetBytes(boundary);
            var parts = new List<MultipartPart>();
            var position = IndexOf(body, boundaryBytes, 0);
            while (position >= 0)
            {
                var start = position + boundaryBytes.Length;
                if (start + 2 <= body.Length && body[start] == '-' && body[start + 1] == '-')
                {
                    break;
                }

                start += 2; // CRLF after the boundary
                var next = IndexOf(body, boundaryBytes, start);
                var end = next < 0 ? body.Length : next - 2; // CRLF before the next boundary
                var headersEnd = IndexOf(body, Encoding.ASCII.GetBytes("\r\n\r\n"), start);
                var part = new MultipartPart();
                foreach (var line in Encoding.UTF8.GetString(body, start, headersEnd - start).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = line.IndexOf(':');
                    part.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
                }

                var bodyStart = headersEnd + 4;
                part.Body = body.Skip(bodyStart).Take(Math.Max(0, end - bodyStart)).ToArray();
                parts.Add(part);
                position = next;
            }

            return parts;
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int from)
        {
            for (var i = from; i <= haystack.Length - needle.Length; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
