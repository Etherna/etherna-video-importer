using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class UriResource : Uri
    {
        // Fields.
        private (byte[], Encoding?)? onlineResourceCache;

        // Constructors.
        public UriResource(
            string uri,
            UriKind allowedUriKinds = UriKind.All)
            : base(uri, allowedUriKinds) { }

        // Methods.
        public async Task<bool> ExistsAsync(bool useOnlineCache, UriKind allowedUriKinds = UriKind.All, string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (!useOnlineCache)
                onlineResourceCache = null;
            if (onlineResourceCache != null)
                return true;

            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case UriKind.LocalAbsolute:
                    return File.Exists(absoluteUri) || Directory.Exists(absoluteUri);

                case UriKind.OnlineAbsolute:
                    var onlineContent = await TryGetOnlineAsByteArrayAsync(absoluteUri);

                    if (onlineContent != null && useOnlineCache)
                        onlineResourceCache = onlineContent;

                    return onlineContent != null;

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        public async Task<byte[]> ReadAsByteArrayAsync(bool useOnlineCache, UriKind allowedUriKinds = UriKind.All, string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (!useOnlineCache)
                onlineResourceCache = null;
            if (onlineResourceCache != null)
                return onlineResourceCache.Value.Item1;

            // Get resource.
            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case UriKind.LocalAbsolute:
                    return await File.ReadAllBytesAsync(absoluteUri);

                case UriKind.OnlineAbsolute:
                    var onlineResource = await TryGetOnlineAsByteArrayAsync(absoluteUri) ??
                        throw new IOException($"Can't retrieve online resource at {absoluteUri}");

                    if (useOnlineCache)
                        onlineResourceCache = onlineResource;

                    return onlineResource.Item1;

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        public async Task<string> ReadAsStringAsync(bool useOnlineCache, UriKind allowedUriKinds = UriKind.All, string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (!useOnlineCache)
                onlineResourceCache = null;
            if (onlineResourceCache != null)
            {
                var (onlineContent, onlineContentEncoding) = onlineResourceCache.Value;
                onlineContentEncoding ??= Encoding.UTF8;
                return onlineContentEncoding.GetString(onlineContent);
            }

            // Get resource.
            var (absoluteUri, absoluteUriKind) = ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case UriKind.LocalAbsolute:
                    return await File.ReadAllTextAsync(absoluteUri);

                case UriKind.OnlineAbsolute:
                    var onlineResource = await TryGetOnlineAsByteArrayAsync(absoluteUri) ??
                        throw new IOException($"Can't retrieve online resource at {absoluteUri}");
                    
                    if (useOnlineCache)
                        onlineResourceCache = onlineResource;

                    var (onlineContent, onlineContentEncoding) = onlineResource;
                    onlineContentEncoding ??= Encoding.UTF8;
                    return onlineContentEncoding.GetString(onlineContent);

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        // Helpers.
        private static async Task<(byte[], Encoding?)?> TryGetOnlineAsByteArrayAsync(string onlineAbsoluteUri)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(onlineAbsoluteUri);
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

                return (content, contentEncoding);
            }
            catch
            {
                return null;
            }
        }
    }
}
