// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class SourceFile : FileBase
    {
        // Fields.
        private (byte[], Encoding?)? onlineResourceCache;

        // Constructor.
        public SourceFile(
            SourceUri fileUri,
            IHttpClientFactory httpClientFactory)
        {
            FileUri = fileUri;
            HttpClientFactory = httpClientFactory;
        }

        // Properties.
        public SourceUri FileUri { get; }

        // Protected properties.
        protected IHttpClientFactory HttpClientFactory { get; }

        // Methods.
        public void ClearOnlineCache() => onlineResourceCache = null;

        public async Task<bool> ExistsAsync(
            bool useCacheIfOnline = false,
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (useCacheIfOnline && onlineResourceCache != null)
                return true;

            var (absoluteUri, absoluteUriKind) = FileUri.ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case SourceUriKind.LocalAbsolute:
                    return File.Exists(absoluteUri) || Directory.Exists(absoluteUri);

                case SourceUriKind.OnlineAbsolute:
                    // Try to get resource byte size with an HEAD request.
                    var byteSyze = await TryGetOnlineByteSizeWithHeadRequestAsync(absoluteUri);
                    if (byteSyze != null)
                        return true;

                    // Otherwise, try to download it.
                    var onlineContent = await TryGetOnlineAsByteArrayAsync(absoluteUri);
                    if (onlineContent != null && useCacheIfOnline)
                        onlineResourceCache = onlineContent;

                    return onlineContent != null;

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        public override Task<long> GetByteSizeAsync() => GetByteSizeAsync(false, SourceUriKind.All, null);

        public async Task<long> GetByteSizeAsync(
            bool useCacheIfOnline = false,
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (useCacheIfOnline && onlineResourceCache != null)
                return onlineResourceCache.Value.Item1.LongLength;

            // Get resource size.
            var (absoluteUri, absoluteUriKind) = FileUri.ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case SourceUriKind.LocalAbsolute:
                    return new FileInfo(absoluteUri).Length;

                case SourceUriKind.OnlineAbsolute:
                    // Try to get resource byte size with an HEAD request.
                    var byteSyze = await TryGetOnlineByteSizeWithHeadRequestAsync(absoluteUri);
                    if (byteSyze.HasValue)
                        return byteSyze.Value;

                    // Otherwise, try to download it.
                    var onlineResource = await TryGetOnlineAsByteArrayAsync(absoluteUri) ??
                        throw new IOException($"Can't retrieve online resource at {absoluteUri}");

                    if (useCacheIfOnline)
                        onlineResourceCache = onlineResource;

                    return onlineResource.Item1.LongLength;

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        public async Task<(byte[] ByteArray, Encoding? Encoding)> ReadToByteArrayAsync(
            bool useCacheIfOnline = false,
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? baseDirectory = null)
        {
            // Use cache if enabled and available.
            if (useCacheIfOnline && onlineResourceCache != null)
                return onlineResourceCache.Value;

            // Get resource.
            var (absoluteUri, absoluteUriKind) = FileUri.ToAbsoluteUri(allowedUriKinds, baseDirectory);
            switch (absoluteUriKind)
            {
                case SourceUriKind.LocalAbsolute:
                    return (await File.ReadAllBytesAsync(absoluteUri), null);

                case SourceUriKind.OnlineAbsolute:
                    var onlineResource = await TryGetOnlineAsByteArrayAsync(absoluteUri) ??
                        throw new IOException($"Can't retrieve online resource at {absoluteUri}");

                    if (useCacheIfOnline)
                        onlineResourceCache = onlineResource;

                    return onlineResource;

                default: throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute");
            }
        }

        public async Task<(Stream Stream, Encoding? Encoding)> ReadToStreamAsync(
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? baseDirectory = null)
        {
            // Get resource.
            var (absoluteUri, absoluteUriKind) = FileUri.ToAbsoluteUri(allowedUriKinds, baseDirectory);
            return absoluteUriKind switch
            {
                SourceUriKind.LocalAbsolute => (File.OpenRead(absoluteUri), null),

                SourceUriKind.OnlineAbsolute => await TryGetOnlineAsStreamAsync(absoluteUri)
                    ?? throw new IOException($"Can't retrieve online resource at {absoluteUri}"),

                _ => throw new InvalidOperationException("Invalid absolute uri kind. It should be well defined and absolute"),
            };
        }

        public async Task<string> ReadToStringAsync(
            bool useCacheIfOnline = false,
            SourceUriKind allowedUriKinds = SourceUriKind.All,
            string? baseDirectory = null)
        {
            var (content, encoding) = await ReadToByteArrayAsync(useCacheIfOnline, allowedUriKinds, baseDirectory);
            encoding ??= Encoding.UTF8;
            return encoding.GetString(content);
        }

        public void SetSwarmHash(SwarmHash hash)
        {
            if (SwarmHash != null)
                throw new InvalidOperationException("Swarm hash already set");
            SwarmHash = hash;
        }

        public string? TryGetFileName()
        {
            if (FileUri.OriginalUri.EndsWith('/') ||
                FileUri.OriginalUri.EndsWith('\\'))
                return null;
            return FileUri.OriginalUri.Split('/', '\\').Last();
        }

        // Helpers.
        private async Task<(byte[], Encoding?)?> TryGetOnlineAsByteArrayAsync(
            string onlineAbsoluteUri)
        {
            var result = await TryGetOnlineAsStreamAsync(onlineAbsoluteUri);
            if (result is null)
                return null;

            var (contentStream, encoding) = result.Value;
            var byteArrayContent = contentStream.ToArray();
            await contentStream.DisposeAsync();

            return (byteArrayContent, encoding);
        }

        private async Task<(MemoryStream, Encoding?)?> TryGetOnlineAsStreamAsync(
            string onlineAbsoluteUri)
        {
            try
            {
                using var httpClient = HttpClientFactory.CreateClient();
                using var response = await httpClient.GetAsync(onlineAbsoluteUri);
                if (!response.IsSuccessStatusCode)
                    return null;

                // Get content with encoding.
                using var contentStream = await response.Content.ReadAsStreamAsync();
                Encoding? contentEncoding = null;

                // Copy stream to memory stream.
                var memoryStream = new MemoryStream();
                await contentStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Try to extract the encoding from the Content-Type header.
                if (response.Content.Headers.ContentType?.CharSet != null)
                {
                    try { contentEncoding = Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet); }
                    catch (ArgumentException) { }
                }

                return (memoryStream, contentEncoding);
            }
            catch
            {
                return null;
            }
        }

        private async Task<long?> TryGetOnlineByteSizeWithHeadRequestAsync(string absoluteUri)
        {
            try
            {
                using var httpClient = HttpClientFactory.CreateClient();
                using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, absoluteUri);
                using var response = await httpClient.SendAsync(httpRequestMessage);

                if (response.Headers.TryGetValues("Content-Length", out var values))
                {
                    string contentLength = values.GetEnumerator().Current;
                    if (long.TryParse(contentLength, out var byteSize))
                        return byteSize;
                }
            }
            catch { }

            return default;
        }
    }
}
