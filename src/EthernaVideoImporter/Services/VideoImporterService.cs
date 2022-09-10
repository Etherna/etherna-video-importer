using EthernaVideoImporter.Dtos;
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
        public async Task Start(VideoDataInfoDto videoDataInfoDto)
        {
            if (videoDataInfoDto.VideoStatus == VideoStatus.Processed)
                return;
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            if (string.IsNullOrWhiteSpace(videoDataInfoDto.DownloadedFileName))
            {
                // Take best video resolution.
                var videos = await youtubeDownloadClient.GetAllVideosAsync(videoDataInfoDto.YoutubeUrl);
                var videoWithAudio = videos
                    .Where(i => i.AudioBitrate != -1); 
                var videoDownload = videoWithAudio
                    .First(i => i.AudioBitrate == videoWithAudio.Max(j => j.AudioBitrate)); // Take best resolution
                Console.WriteLine($"Resolution: {videoDownload.Resolution}\tAudio Bitrate: {videoDownload.AudioBitrate}");

                //Start download
                videoDataInfoDto.VideoStatus = VideoStatus.Downloading;
                await youtubeDownloadClient
                    .CreateDownloadAsync(
                    new Uri(videoDownload.Uri),
                    Path.Combine(tmpFolder, videoDownload.FullName),
                    new Progress<Tuple<long, long>>((Tuple<long, long> v) =>
                    {
                        var percent = (int)((v.Item1 * 100) / v.Item2);
                        Console.Write(string.Format("Downloading.. ( % {0} ) {1} / {2} MB\r", percent, (v.Item1 / (double)(1024 * 1024)).ToString("N"), (v.Item2 / (double)(1024 * 1024)).ToString("N")));
                    }));
                Console.WriteLine("");
                videoDataInfoDto.DownloadedFileName = videoDownload.FullName;
                videoDataInfoDto.VideoStatus = VideoStatus.Downloaded;
                videoDataInfoDto.VideoStatusNote = "";
            }

            //
            //TODO UPLOAD

            videoDataInfoDto.VideoStatus = VideoStatus.Processed;
            return;
        }
    }
}
