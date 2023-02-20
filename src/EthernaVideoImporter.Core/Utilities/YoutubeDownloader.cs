using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Core.Utilities
{
    public class YoutubeDownloader
    {
        // Fields.
        private readonly DirectoryInfo downloadDirectory;
        private readonly YoutubeClient youtubeClient;

        // Constructor.
        public YoutubeDownloader(YoutubeClient youtubeClient)
        {
            downloadDirectory = Directory.CreateTempSubdirectory();
            this.youtubeClient = youtubeClient;
        }

        // Methods.
        public async Task<AudioFile> DownloadAudioTrackAsync(
            IStreamInfo audioStream,
            string videoTitle)
        {
            if (audioStream is null)
                throw new ArgumentNullException(nameof(audioStream));

            var audioFileName = $"{videoTitle.ToSafeFileName()}_onlyaudio.{audioStream.Container}";
            var audioFilePath = Path.Combine(downloadDirectory.FullName, audioFileName);

            for (int i = 0; i <= CommonConsts.DOWNLOAD_MAX_RETRY; i++)
            {
                if (i == CommonConsts.DOWNLOAD_MAX_RETRY)
                    throw new InvalidOperationException($"Some error during download of audio track");

                try
                {
                    await youtubeClient.Videos.Streams.DownloadAsync(
                        audioStream,
                        audioFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading audio track ({(progressStatus * 100):N0}%) {audioStream.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    break;
                }
                catch { await Task.Delay(CommonConsts.DOWNLOAD_RETRY_TIMESPAN).ConfigureAwait(false); }
            }

            return new AudioFile(
                audioFilePath,
                audioStream.Size.Bytes);
        }

        public async Task<ThumbnailFile> DownloadThumbnailAsync(
            Thumbnail thumbnail,
            string videoTitle)
        {
            if (thumbnail is null)
                throw new ArgumentNullException(nameof(thumbnail));

            string thumbnailFilePath = Path.Combine(downloadDirectory.FullName, $"{videoTitle.ToSafeFileName()}_thumb.jpg");
            long fileSize = 0;

            for (int i = 0; i <= CommonConsts.DOWNLOAD_MAX_RETRY; i++)
            {
                if (i == CommonConsts.DOWNLOAD_MAX_RETRY)
                    throw new InvalidOperationException($"Some error during download of thumbnail");

                try
                {
                    using var httpClient = new HttpClient();
                    var stream = await httpClient.GetStreamAsync(thumbnail.Url).ConfigureAwait(false);
                    using var fileStream = new FileStream(thumbnailFilePath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                    fileSize = fileStream.Length;
                    break;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            }

            return new ThumbnailFile(
                thumbnailFilePath,
                fileSize,
                thumbnail.Resolution.Width,
                thumbnail.Resolution.Height);
        }

        public async Task<VideoFile> DownloadVideoStreamAsync(
            IVideoStreamInfo videoStream,
            string videoTitle)
        {
            if (videoStream is null)
                throw new ArgumentNullException(nameof(videoStream));

            var videoFileName = $"{videoTitle.ToSafeFileName()}_{videoStream.VideoResolution}.{videoStream.Container}";
            var videoFilePath = Path.Combine(downloadDirectory.FullName, videoFileName);
            var videoQualityLabel = videoStream.VideoQuality.Label;

            // Download video.
            for (int i = 0; i <= CommonConsts.DOWNLOAD_MAX_RETRY; i++)
            {
                if (i == CommonConsts.DOWNLOAD_MAX_RETRY)
                    throw new InvalidOperationException($"Some error during download of video");

                try
                {
                    await youtubeClient.Videos.Streams.DownloadAsync(
                        videoStream,
                        videoFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading resolution {videoQualityLabel} ({(progressStatus * 100):N0}%) {videoStream.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    break;
                }
                catch { await Task.Delay(CommonConsts.DOWNLOAD_RETRY_TIMESPAN).ConfigureAwait(false); }
            }

            return new VideoFile(
                videoFilePath,
                videoQualityLabel,
                videoStream.Size.Bytes);
        }
    }
}
