using Etherna.EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Dtos;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDownloader.Clients;

namespace EthernaVideoImporter.Services
{
    internal class VideoImporterService
    {
        private readonly YoutubeDownloadClient youtubeDownloadClient;
        private readonly string tmpFolder;

        // Constractor.
        public VideoImporterService(
            YoutubeDownloadClient youtubeDownloadClient,
            string tmpFolder)
        {
            this.youtubeDownloadClient = youtubeDownloadClient;
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task<VideoDownloadInfo?> Start(VideoDataInfoDto videoDataInfoDto)
        {
            if (videoDataInfoDto.VideoStatus == VideoStatus.Processed)
                return null;
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            try
            {
                // Take best video resolution.
                var videos = await youtubeDownloadClient.GetAllVideosAsync(videoDataInfoDto.YoutubeUrl);
                var videoWithAudio = videos
                    .Where(i => i.AudioBitrate != -1);
                var videoDownload = videoWithAudio
                    .First(i => i.AudioBitrate == videoWithAudio.Max(j => j.AudioBitrate)); // Take best resolution
                Console.WriteLine($"Resolution: {videoDownload.Resolution}\tAudio Bitrate: {videoDownload.AudioBitrate}");

                // Start download.
                videoDataInfoDto.VideoStatus = VideoStatus.Downloading;
                videoDataInfoDto.DownloadedFilePath = Path.Combine(tmpFolder, videoDownload.FullName);
                await youtubeDownloadClient
                    .CreateDownloadAsync(
                    new Uri(videoDownload.Uri),
                    videoDataInfoDto.DownloadedFilePath,
                    new Progress<Tuple<long, long>>((Tuple<long, long> v) =>
                    {
                        var percent = (int)((v.Item1 * 100) / v.Item2);
#pragma warning disable CA1305 // Specify IFormatProvider
                        Console.Write(string.Format("Downloading.. ( % {0} ) {1} / {2} MB\r", percent, (v.Item1 / (double)(1024 * 1024)).ToString("N"), (v.Item2 / (double)(1024 * 1024)).ToString("N")));
#pragma warning restore CA1305 // Specify IFormatProvider
                    }));
                Console.WriteLine("");
                videoDataInfoDto.DownloadedFileName = videoDownload.FullName;
                videoDataInfoDto.VideoStatus = VideoStatus.Downloaded;
                videoDataInfoDto.VideoStatusNote = "";

                // Result VideoDownloadInfo.
                var fileSize = new FileInfo(videoDataInfoDto.DownloadedFilePath).Length;
                return new VideoDownloadInfo(
                    (int)Math.Ceiling(fileSize / (videoDataInfoDto.Duration / 60 * 0.0075)),  //TODO it's OK?  bitrate = file size / (number of minutes * .0075)
                    videoDownload.Resolution.ToString(CultureInfo.InvariantCulture) + "p",
                    fileSize);
            }
            catch (Exception)
            {
                videoDataInfoDto.DownloadedFileName = "";
                videoDataInfoDto.DownloadedFilePath = "";
                videoDataInfoDto.VideoStatus = VideoStatus.Downloading;
                throw;
            }
        }
    }
}
