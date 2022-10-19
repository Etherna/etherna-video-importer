using Etherna.EthernaVideoImporter.Models;
using EthernaVideoImporter.CommonData.Models;
using EthernaVideoImporter.CommonData.Services;
using EthernaVideoImporter.Models;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using VideoLibrary;

namespace EthernaVideoImporter.Services
{
    internal class VideoImporterService
    {
        private readonly IDownloadClient downloadClient;
        private readonly int? maxFilesize;
        private readonly string tmpFolder;

        // Constractor.
        public VideoImporterService(
            IDownloadClient downloadClient,
            string tmpFolder,
            int? maxFilesize)
        {
            this.downloadClient = downloadClient;
            this.maxFilesize = maxFilesize;
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task StartAsync(VideoInfoWithData videoDataInfoDto)
        {
            if ((File.Exists(videoDataInfoDto.DownloadedFilePath) ||
                !string.IsNullOrWhiteSpace(videoDataInfoDto.VideoReference)) /*&&
                videoDataInfoDto.CsvItemStatus != CsvItemStatus.MetadataModified*/)
                return;
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            try
            {
                // Take best video resolution.
                SourceVideoInfo? videoDownload = null;
                var reTry = 0;
                while (videoDownload is null &&
                        reTry < 5)
                {
                    try
                    {
                        videoDownload = await downloadClient.FirstVideoWithBestResolutionAsync(videoDataInfoDto.YoutubeUrl, maxFilesize).ConfigureAwait(false);
                    }
                    catch (Exception) { }
                    finally
                    {
                        reTry++;
                    }
                }
                if (videoDownload is null)
                {
                    var ex = new InvalidOperationException("Video not found");
                    ex.Data.Add("Url", videoDataInfoDto.YoutubeUrl);
                    throw ex;
                }

                // Start download and show progress.
                videoDataInfoDto.DownloadedFilePath = Path.Combine(tmpFolder, videoDownload.Filename);
                await downloadClient
                    .DownloadAsync(
                    new Uri(videoDownload.Uri),
                    videoDataInfoDto.DownloadedFilePath,
                    new Progress<Tuple<long, long>>((Tuple<long, long> v) =>
                    {
                        var percent = (int)(v.Item1 * 100 / v.Item2);
                        Console.Write($"Downloading.. ( % {percent} ) {v.Item1 / (1024 * 1024)} / {v.Item2 / (1024 * 1024)} MB\r");
                    })).ConfigureAwait(false);
                Console.WriteLine("");

                // Set VideoDataInfoDto from downloaded video.
                var fileSize = new FileInfo(videoDataInfoDto.DownloadedFilePath!).Length;
                videoDataInfoDto.DownloadedFileName = videoDownload.Filename;
                videoDataInfoDto.VideoStatusNote = "";
                videoDataInfoDto.Quality = videoDownload.Resolution.ToString(CultureInfo.InvariantCulture) + "p";
                videoDataInfoDto.Size = fileSize;

                var tmpDuration = videoDataInfoDto.Duration;
                videoDataInfoDto.Duration = GetDuration(videoDataInfoDto.DownloadedFilePath);
                if (tmpDuration > 0 &&
                    Math.Abs(tmpDuration - videoDataInfoDto.Duration) > 10)
                {
                    throw new InvalidOperationException($"Invalid Duration tmpDuration: {tmpDuration}\t Duration: {videoDataInfoDto.Duration}");
                }
                videoDataInfoDto.Bitrate = (int)Math.Ceiling((double)fileSize * 8 / videoDataInfoDto.Duration);

                // Download Thumbnail.
                videoDataInfoDto.DownloadedThumbnailPath = await DownloadThumbnailAsync(videoDownload.VideoId, tmpFolder).ConfigureAwait(false);

                videoDataInfoDto.ImportStatus = ImportStatus.Downloaded;
            }
            catch (Exception)
            {
                videoDataInfoDto.DownloadedFileName = "";
                videoDataInfoDto.DownloadedFilePath = "";
                throw;
            }
        }

        // Private Methods.
        private async Task<string?> DownloadThumbnailAsync(string? videoId, string tmpFolder)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            var filePath = $"{tmpFolder}/{videoId}.jpg";
            using var httpClient = new HttpClient();
            var streamGot = await httpClient.GetStreamAsync($"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg").ConfigureAwait(false);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await streamGot.CopyToAsync(fileStream).ConfigureAwait(false);

            return filePath;
        }
        public static int GetDuration(string? pathToVideoFile)
        {
            if (string.IsNullOrWhiteSpace(pathToVideoFile))
                return 0;
            var ffProbe = new NReco.VideoInfo.FFProbe();
            var videoInfo = ffProbe.GetMediaInfo(pathToVideoFile);
            return (int)Math.Ceiling(videoInfo.Duration.TotalSeconds);
        }
    }
}
