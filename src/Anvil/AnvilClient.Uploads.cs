using System.IO;

namespace Anvil
{
    public partial class AnvilClient
    {
        /// <summary>
        /// Prepares a file on disk for a multipart upload with a GraphQL mutation.
        /// Put the result anywhere in the mutation's variables under a <c>file</c>
        /// key, e.g. <see cref="Payloads.DocumentUpload.File"/> in
        /// <see cref="CreateEtchPacketAsync"/>, or <c>$file: Upload!</c> of
        /// <c>createCast</c> with <see cref="RequestGraphQLAsync(string, object?, string?, RequestOptions?, System.Threading.CancellationToken)"/>.
        /// Like node-anvil's <c>Anvil.prepareGraphQLFile</c>.
        /// </summary>
        /// <param name="path">The file to upload</param>
        /// <param name="filename">The filename the API sees; defaults to the file's name</param>
        /// <param name="mimetype">The file's content type; guessed from the filename when not given</param>
        public static GraphQLUpload PrepareGraphQLFile(string path, string? filename = null, string? mimetype = null)
        {
            return GraphQLUpload.FromPath(path, filename, mimetype);
        }

        /// <summary>Prepares a file in memory for a multipart upload. See <see cref="PrepareGraphQLFile(string, string?, string?)"/>.</summary>
        public static GraphQLUpload PrepareGraphQLFile(byte[] bytes, string filename, string? mimetype = null)
        {
            return GraphQLUpload.FromBytes(bytes, filename, mimetype);
        }

        /// <summary>
        /// Prepares a file read from a stream for a multipart upload. See
        /// <see cref="PrepareGraphQLFile(string, string?, string?)"/>. A stream that
        /// can't seek can only be sent once, so a retry after a 429 fails.
        /// </summary>
        public static GraphQLUpload PrepareGraphQLFile(Stream stream, string? filename = null, string? mimetype = null)
        {
            return GraphQLUpload.FromStream(stream, filename, mimetype);
        }
    }
}
