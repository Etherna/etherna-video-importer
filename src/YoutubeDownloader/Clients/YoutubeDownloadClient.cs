using System.Net.Http.Headers;
using VideoLibrary;
using YoutubeDownloader.Handlers;

namespace YoutubeDownloader.Clients
{

    public class YoutubeDownloadClient : YouTube
    {
        private readonly HttpClient _client = new();
        private readonly long _chunkSize = 10_485_760;
        private long _fileSize = 0L;

        // Public Methods.
        public async Task CreateDownloadAsync(Uri uri, string filePath, IProgress<Tuple<long, long>> progress)
        {
            var totalBytesCopied = 0L;
            _fileSize = await GetContentLengthAsync(uri.AbsoluteUri) ?? 0;
            if (_fileSize == 0)
            {
                throw new Exception("File has no any content !");
            }
            using var output = File.OpenWrite(filePath);
            var segmentCount = (int)Math.Ceiling(1.0 * _fileSize / _chunkSize);
            for (var i = 0; i < segmentCount; i++)
            {
                var from = i * _chunkSize;
                var to = (i + 1) * _chunkSize - 1;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using (request)
                {
                    // Download Stream
                    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                        response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsStreamAsync();
                    //File Steam
                    var buffer = new byte[81920];
                    int bytesCopied;
                    do
                    {
                        bytesCopied = await stream.ReadAsync(buffer);
                        output.Write(buffer, 0, bytesCopied);
                        totalBytesCopied += bytesCopied;
                        progress.Report(new Tuple<long, long>(totalBytesCopied, _fileSize));
                    } while (bytesCopied > 0);
                }
            }
        }

        // Protected Methods.
        protected override HttpClient MakeClient(HttpMessageHandler handler)
        {
            return base.MakeClient(handler);
        }

        protected override HttpMessageHandler MakeHandler()
        {
            return YoutubeHandler.GetHandler();
        }

        // Private Methods.
        private async Task<long?> GetContentLengthAsync(string requestUri, bool ensureSuccess = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (ensureSuccess)
                response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength;
        }
    }


}