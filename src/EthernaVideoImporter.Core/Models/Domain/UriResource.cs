using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Better use string here")]
    public class UriResource
    {
        // Fields.
        private (byte[], Encoding?)? onlineResourceCache;

        // Constructors.
        public UriResource(
            string uri)
        {
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("Uri cannot be null or empty", nameof(uri));

            OriginalUri = uri;

            // Define uri type.
            if (Uri.TryCreate(uri, UriKind.Absolute, out var _)) //if is an online URL
            {
                UriType = UriType.OnlineAbsolute;
            }
            else //if is a local path
            {
                if (uri.Intersect(Path.GetInvalidPathChars()).Any())
                    throw new ArgumentException("Uri contains invalid chars for a local path", nameof(uri));

                // Check if it's an absolute path.
                if (Path.IsPathRooted(uri))
                    UriType = UriType.LocalAbsolute;

                // If it's not a URL or an absolute path, it's a relative path.
                UriType = UriType.LocalRelative;
            }
        }

        // Properties.
        public string OriginalUri { get; }
        public string ParentDirectoryAbsoluteUri
        {
            get
            {
                switch (UriType)
                {
                    case UriType.LocalAbsolute:
                    case UriType.LocalRelative:
                        return Path.GetDirectoryName(ToAbsoluteUri())!;
                    case UriType.OnlineAbsolute:
                        var lastSegment = new Uri(OriginalUri, UriKind.Absolute).Segments.Last();
                        return lastSegment.EndsWith("/", StringComparison.OrdinalIgnoreCase) ?
                            OriginalUri :
                            OriginalUri[..^lastSegment.Length];
                    default: throw new InvalidOperationException();
                }
            }
        }
        public UriType UriType { get; }

        // Methods.
        public async Task<bool> ExistsAsync(bool useOnlineCache, string? baseDirectory = null)
        {
            // Use cache if available and enabled.
            if (!useOnlineCache)
                onlineResourceCache = null;
            if (onlineResourceCache != null)
                return true;

            // Search if resource exists.
            switch (UriType)
            {
                case UriType.LocalAbsolute:
                case UriType.LocalRelative:
                    return File.Exists(ToAbsoluteUri(baseDirectory));
                case UriType.OnlineAbsolute:
                    var content = await TryGetOnlineResourceAsByteArrayAsync(useOnlineCache);
                    return content != null;
                default: throw new InvalidOperationException();
            }
        }

        public string ToAbsoluteUri(string? baseDirectory = null) =>
            UriType switch
            {
                UriType.LocalAbsolute => OriginalUri,
                UriType.LocalRelative => Path.GetFullPath(
                    OriginalUri,
                    baseDirectory ?? Directory.GetCurrentDirectory()),
                UriType.OnlineAbsolute => OriginalUri,
                _ => throw new InvalidOperationException()
            };

        public async Task<string> ReadResourceAsStringAsync(bool useOnlineCache)
        {
            switch (UriType)
            {
                case UriType.LocalAbsolute:
                case UriType.LocalRelative:
                    return await File.ReadAllTextAsync(ToAbsoluteUri());

                case UriType.OnlineAbsolute:
                    var (onlineContent, onlineContentEncoding) = await TryGetOnlineResourceAsByteArrayAsync(useOnlineCache)
                        ?? throw new InvalidOperationException("Can't find resource online");
                    onlineContentEncoding ??= Encoding.UTF8;

                    return onlineContentEncoding.GetString(onlineContent);

                default: throw new InvalidOperationException();
            }
        }

        // Helpers.
        private async Task<(byte[], Encoding?)?> TryGetOnlineResourceAsByteArrayAsync(bool useCache)
        {
            if (UriType != UriType.OnlineAbsolute)
                throw new InvalidOperationException();

            // Try to use cache if available.
            if (!useCache)
                onlineResourceCache = null;
            if (onlineResourceCache != null)
                return onlineResourceCache;

            // Perform actual request.
            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(OriginalUri);
                if (!response.IsSuccessStatusCode)
                    return null;

                // Get content with encoding.
                var content = await response.Content.ReadAsByteArrayAsync();
                Encoding? contentEncoding = null;

                // Try to extract the encoding from the Content-Type header.
                if (response.Content.Headers.ContentType?.CharSet != null)
                {
                    try { contentEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet); }
                    catch (ArgumentException) { }
                }

                if (useCache)
                    onlineResourceCache = (content, contentEncoding);

                return (content, contentEncoding);
            }
            catch
            {
                return null;
            }
        }
    }
}
