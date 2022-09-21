using Etherna.EthernaVideoImporter.Dtos;
using Etherna.EthernaVideoImporter.Services;
using System.Net.Http.Headers;
using VideoLibrary;

namespace YoutubeDownloader.Clients
{

    public class YoutubeDownloadClient : YouTube, IDownloadClient
    {
        private readonly HttpClient client = new();
        private readonly long chunkSize = 10_485_760;

        // Public Methods.
        public async Task DownloadAsync(
            Uri uri, 
            string filePath, 
            IProgress<Tuple<long, long>> progress)
        {
            var fileSize = await GetContentLengthAsync(uri.AbsoluteUri) ?? 0;
            if (fileSize == 0)
            {
                throw new Exception("File has no any content !");
            }
            using var output = File.OpenWrite(filePath);
            var segmentCount = (int)Math.Ceiling(1.0 * fileSize / chunkSize);
            for (var i = 0; i < segmentCount; i++)
            {
                var from = i * chunkSize;
                var to = (i + 1) * chunkSize - 1;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using (request)
                {
                    var totalBytesCopied = 0L;
                    // Download Stream
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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
                        progress.Report(new Tuple<long, long>(totalBytesCopied, fileSize));
                    } while (bytesCopied > 0);
                }
            }
        }

        public async Task<SourceVideoInfo> FirstVideoWithBestResolutionAsync(string url)
        {
            var videos = await GetAllVideosAsync(url).ConfigureAwait(false);
            var videoWithAudio = videos
                .Where(i => i.AudioBitrate != -1);
            var videoDownload = videoWithAudio
                .First(i => i.AudioBitrate == videoWithAudio.Max(j => j.AudioBitrate)); // Take best resolution

            return new SourceVideoInfo(
                videoDownload.AudioBitrate,
                videoDownload.FullName,
                videoDownload.Resolution,
                videoDownload.Uri);
        }

        // Private Methods.
        private async Task<long?> GetContentLengthAsync(string requestUri, bool ensureSuccess = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (ensureSuccess)
                response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength;
        }
    }

}