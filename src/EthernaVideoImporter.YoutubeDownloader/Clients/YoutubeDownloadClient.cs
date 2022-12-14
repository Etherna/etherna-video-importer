using EthernaVideoImporter.CommonData.Models;
using EthernaVideoImporter.CommonData.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using VideoLibrary;

namespace Etherna.EthernaVideoImporter.YoutubeDownloader
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
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            var fileSize = await GetContentLengthAsync(uri.AbsoluteUri).ConfigureAwait(false) ?? 0;
            if (fileSize == 0)
            {
                throw new InvalidOperationException("File has no any content !");
            }
            using var output = File.OpenWrite(filePath);
            var segmentCount = (int)Math.Ceiling(1.0 * fileSize / chunkSize);
            var totalBytesCopied = 0L;
            for (var i = 0; i < segmentCount; i++)
            {
                var from = i * chunkSize;
                var to = (i + 1) * chunkSize - 1;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using (request)
                {
                    // Download Stream
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    //File Steam
                    var buffer = new byte[81920];
                    int bytesCopied;
                    do
                    {
                        bytesCopied = await stream.ReadAsync(buffer).ConfigureAwait(false);
                        await output.WriteAsync(buffer.AsMemory(0, bytesCopied)).ConfigureAwait(false);
                        totalBytesCopied += bytesCopied;
                        if (progress is not null)
                            progress.Report(new Tuple<long, long>(totalBytesCopied, fileSize));
                    } while (bytesCopied > 0);
                }
            }
        }

        public async Task<SourceVideoInfo> FirstVideoWithBestResolutionAsync(
            string url, 
            int? maxFilesize)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(url);
            var videos = await GetAllVideosAsync(url).ConfigureAwait(false);

            // Take best resolution with audio.
            var videoWithAudio = videos
                .Where(video => video.AudioBitrate != -1 &&
                                video.Resolution > 0)
                .ToList();
            var allResolutions = videoWithAudio
                .Select(video => video.Resolution)
                .Distinct()
                .OrderByDescending(res => res);

            long? bestFilesizeAccepted = null;
            foreach (var currentRes in allResolutions)
            {
                var videoDownload = videoWithAudio
                .First(video => video.Resolution == currentRes);

                bestFilesizeAccepted = await GetContentLengthAsync(videoDownload.Uri, true).ConfigureAwait(false);
                if (maxFilesize is not null &&
                    bestFilesizeAccepted > maxFilesize * 1024 * 1024)
                    continue;

                return new SourceVideoInfo(
                    videoDownload.AudioBitrate,
                    videoDownload.FullName,
                    videoDownload.Resolution,
                    GetVideoIdFromUrl(url),
                    videoDownload.Uri);
            }

            throw new InvalidOperationException($"Not found video less than {maxFilesize}Mb, the best size found is: {bestFilesizeAccepted / 1024 / 1024} Mb");
        }

        // Private Methods.
        private async Task<long?> GetContentLengthAsync(string requestUri, bool ensureSuccess = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (ensureSuccess)
                response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength;
        }

        private string? GetVideoIdFromUrl(string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (query != null &&
                query.AllKeys.Contains("v"))
                return query["v"];

            return uri.Segments.Last();
        }
    }

}