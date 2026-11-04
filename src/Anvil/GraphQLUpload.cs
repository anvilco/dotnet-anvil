using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json;

namespace Anvil
{
    /// <summary>
    /// A file to send with a GraphQL mutation as a multipart upload, per the
    /// GraphQL multipart request spec. Create one with
    /// <see cref="AnvilClient.PrepareGraphQLFile(string, string?, string?)"/> and put it
    /// anywhere in the mutation's variables under a <c>file</c> key, e.g.
    /// <see cref="Payloads.DocumentUpload.File"/>. Like node-anvil's <c>UploadWithOptions</c>.
    /// </summary>
    public sealed class GraphQLUpload
    {
        internal const string SentinelPrefix = "__anvil_graphql_upload__:";

        // Uploads are found again, after serialization, by the id in their sentinel
        private static readonly ConcurrentDictionary<Guid, WeakReference<GraphQLUpload>> Registry
            = new ConcurrentDictionary<Guid, WeakReference<GraphQLUpload>>();

        private readonly string? _path;
        private readonly byte[]? _bytes;
        private readonly Stream? _stream;
        private bool _streamRead;

        private GraphQLUpload(string? path, byte[]? bytes, Stream? stream, string filename, string? mimetype)
        {
            _path = path;
            _bytes = bytes;
            _stream = stream;
            Filename = filename;
            Mimetype = mimetype ?? GuessMimetype(filename);
            Registry[Id] = new WeakReference<GraphQLUpload>(this);
        }

        /// <summary>A file on disk. The filename defaults to the file's name.</summary>
        public static GraphQLUpload FromPath(string path, string? filename = null, string? mimetype = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is required", nameof(path));
            }

            return new GraphQLUpload(path, null, null, filename ?? Path.GetFileName(path), mimetype);
        }

        /// <summary>A file in memory.</summary>
        public static GraphQLUpload FromBytes(byte[] bytes, string filename, string? mimetype = null)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return new GraphQLUpload(null, bytes, null, RequireFilename(filename), mimetype);
        }

        /// <summary>
        /// A file read from a stream, which the upload reads from its current
        /// position and does not dispose. A stream that can't seek can only be
        /// sent once, so a retry after a 429 fails; pass a path or bytes to be
        /// able to retry. The filename defaults to a <see cref="FileStream"/>'s name.
        /// </summary>
        public static GraphQLUpload FromStream(Stream stream, string? filename = null, string? mimetype = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (filename == null && stream is FileStream file)
            {
                filename = Path.GetFileName(file.Name);
            }

            return new GraphQLUpload(null, null, stream, RequireFilename(filename), mimetype);
        }

        private static string RequireFilename(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("A filename is required for a file given as bytes or a stream", nameof(filename));
            }

            return filename!;
        }

        /// <summary>Identifies the upload in the serialized variables.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The filename the API sees.</summary>
        public string Filename { get; }

        /// <summary>The content type of the file part. Guessed from the filename when not given.</summary>
        public string Mimetype { get; }

        internal string Sentinel => SentinelPrefix + Id.ToString("N");

        internal static GraphQLUpload? FromSentinel(string value)
        {
            if (!value.StartsWith(SentinelPrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(value.Substring(SentinelPrefix.Length), "N", out var id)
                || !Registry.TryGetValue(id, out var reference)
                || !reference.TryGetTarget(out var upload))
            {
                return null;
            }

            return upload;
        }

        /// <summary>
        /// The file's content for one attempt to send it. A path is opened each
        /// time and a seekable stream rewound, so the file can be sent again
        /// after a 429.
        /// </summary>
        internal HttpContent OpenContent()
        {
            HttpContent content;
            if (_path != null)
            {
                content = new StreamContent(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
            }
            else if (_bytes != null)
            {
                content = new ByteArrayContent(_bytes);
            }
            else
            {
                var stream = _stream!;
                if (_streamRead)
                {
                    if (!stream.CanSeek)
                    {
                        throw new InvalidOperationException($"Can't send {Filename} again because its stream has already been read. Give the file as a path or bytes to be able to retry.");
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                }

                _streamRead = true;
                content = new StreamContent(new NonClosingStream(stream));
            }

            content.Headers.ContentType = new MediaTypeHeaderValue(Mimetype);
            return content;
        }

        private static string GuessMimetype(string filename)
        {
            switch (Path.GetExtension(filename).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".svg": return "image/svg+xml";
                case ".html":
                case ".htm": return "text/html";
                case ".txt": return "text/plain";
                case ".csv": return "text/csv";
                case ".json": return "application/json";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".zip": return "application/zip";
                default: return "application/octet-stream";
            }
        }

        /// <summary>Keeps the caller's stream open when the request content is disposed.</summary>
        private sealed class NonClosingStream : Stream
        {
            private readonly Stream _inner;

            public NonClosingStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    /// <summary>Serializes a <see cref="GraphQLUpload"/> as a sentinel string, which the multipart builder replaces with <c>null</c> and a part.</summary>
    internal sealed class GraphQLUploadConverter : JsonConverter<GraphQLUpload>
    {
        public override void WriteJson(JsonWriter writer, GraphQLUpload? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Sentinel);
        }

        public override GraphQLUpload? ReadJson(JsonReader reader, Type objectType, GraphQLUpload? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("A GraphQLUpload can't be read from JSON");
        }
    }
}
