using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Core.Utilities
{
    public class YoutubeDownloader : IYoutubeDownloader
    {
        // Fields.
        private readonly DirectoryInfo downloadDirectory;
        private readonly string ffMpegPath;
        private readonly YoutubeClient youtubeClient;

        // Constructor.
        public YoutubeDownloader(
            string ffMpegPath,
            YoutubeClient youtubeClient)
        {
            downloadDirectory = Directory.CreateTempSubdirectory();
            this.ffMpegPath = ffMpegPath;
            this.youtubeClient = youtubeClient;
        }

        // Methods.
        public async Task<Video> GetVideoAsync(
            bool includeAudioTrack,
            YouTubeVideoMetadataBase videoMetadata)
        {
            if (videoMetadata is null)
                throw new ArgumentNullException(nameof(videoMetadata));

            // Get manifest data.
            var youtubeStreamsManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoMetadata.Id);

            var videoOnlyStreamsInfo = youtubeStreamsManifest.GetVideoOnlyStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .OrderByDescending(res => res.VideoResolution.Area);
            var audioOnlyStreamInfo = youtubeStreamsManifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .First();

            // Get streams.
            //video streams
            var encodedFiles = new List<FileBase>();
            foreach (var videoOnlyStreamInfo in videoOnlyStreamsInfo)
                encodedFiles.Add(await DownloadVideoStreamAsync(
                    audioOnlyStreamInfo,
                    videoOnlyStreamInfo,
                    videoMetadata.Title));

            //audio only stream
            if (includeAudioTrack)
                encodedFiles.Add(await DownloadAudioTrackAsync(
                    audioOnlyStreamInfo,
                    videoMetadata.Title));

            // Get thumbnail.
            ThumbnailFile? thumbnailFile = null;
            if (videoMetadata.Thumbnail is not null)
                thumbnailFile = await DownloadThumbnailAsync(
                    videoMetadata.Thumbnail,
                    videoMetadata.Title);

            return new Video(videoMetadata, encodedFiles, thumbnailFile);
        }

        // Helpers.
        private async Task<AudioFile> DownloadAudioTrackAsync(
            IStreamInfo audioStream,
            string videoTitle)
        {
            if (audioStream is null)
                throw new ArgumentNullException(nameof(audioStream));

            var audioFileName = $"{videoTitle.ToSafeFileName()}_onlyaudio.{audioStream.Container}";
            var audioFilePath = Path.Combine(downloadDirectory.FullName, audioFileName);

            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some error during download of audio track");

                try
                {
                    await youtubeClient.Videos.Streams.DownloadAsync(
                        audioStream,
                        audioFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading audio track ({(progressStatus * 100):N0}%) {audioStream.Size.MegaBytes:N2} MB\r");
                        }));
                    break;
                }
                catch { await Task.Delay(CommonConsts.DownloadTimespanRetry); }
            }

            return new AudioFile(
                audioFilePath,
                new FileInfo(audioFilePath).Length);
        }

        private async Task<ThumbnailFile> DownloadThumbnailAsync(
            Thumbnail thumbnail,
            string videoTitle)
        {
            if (thumbnail is null)
                throw new ArgumentNullException(nameof(thumbnail));

            string thumbnailFilePath = Path.Combine(downloadDirectory.FullName, $"{videoTitle.ToSafeFileName()}_thumb.jpg");
            
            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some error during download of thumbnail");

                try
                {
                    using var httpClient = new HttpClient();
                    var stream = await httpClient.GetStreamAsync(thumbnail.Url);
                    using var fileStream = new FileStream(thumbnailFilePath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);
                    break;
                }
                catch { await Task.Delay(CommonConsts.DownloadTimespanRetry); }
            }

            return new ThumbnailFile(
                thumbnailFilePath,
                new FileInfo(thumbnailFilePath).Length,
                thumbnail.Resolution.Width,
                thumbnail.Resolution.Height);
        }

        private async Task<VideoFile> DownloadVideoStreamAsync(
            IAudioStreamInfo audioOnlyStream,
            IVideoStreamInfo videoOnlyStream,
            string videoTitle)
        {
            var videoFileName = $"{videoTitle.ToSafeFileName()}_{videoOnlyStream.VideoResolution}.{videoOnlyStream.Container}";
            var videoFilePath = Path.Combine(downloadDirectory.FullName, videoFileName);
            var videoQualityLabel = videoOnlyStream.VideoQuality.Label;

            // Download video.
            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some error during download of video");

                try
                {
                    await youtubeClient.Videos.DownloadAsync(
                        new IStreamInfo[] { audioOnlyStream, videoOnlyStream },
                        new ConversionRequestBuilder(videoFilePath).SetFFmpegPath(ffMpegPath).Build(),
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading and mux resolution {videoQualityLabel} ({(progressStatus * 100):N0}%) " +
                                $"{videoOnlyStream.Size.MegaBytes + audioOnlyStream.Size.MegaBytes:N2} MB\r");
                        }));
                    break;
                }
                catch { await Task.Delay(CommonConsts.DownloadTimespanRetry); }
            }

            return new VideoFile(
                videoFilePath,
                videoQualityLabel,
                new FileInfo(videoFilePath).Length);
        }
    }
}
