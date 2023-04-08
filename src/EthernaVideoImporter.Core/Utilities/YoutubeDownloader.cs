//   Copyright 2022-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
        private readonly IEncoderService encoderService;
        private readonly YoutubeClient youtubeClient;

        // Constructor.
        public YoutubeDownloader(
            IEncoderService encoderService,
            YoutubeClient youtubeClient)
        {
            this.encoderService = encoderService;
            this.youtubeClient = youtubeClient;
        }

        // Methods.
        public async Task<Video> GetVideoAsync(
            YouTubeVideoMetadataBase videoMetadata,
            DirectoryInfo tempDirectory,
            ImporterSettings importerSettings)
        {
            if (videoMetadata is null)
                throw new ArgumentNullException(nameof(videoMetadata));
            if (tempDirectory is null)
                throw new ArgumentNullException(nameof(tempDirectory));
            if (importerSettings is null)
                throw new ArgumentNullException(nameof(importerSettings));

            // Get manifest data.
            var youtubeStreamsManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoMetadata.YoutubeId);

            var videoOnlyStreamInfo = youtubeStreamsManifest.GetVideoOnlyStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .GetWithHighestVideoQuality();

            var audioOnlyStreamInfo = (IAudioStreamInfo)youtubeStreamsManifest.GetAudioOnlyStreams()
                .GetWithHighestBitrate();

            // Get high resolution video.
            var videoLocalFile = await DownloadVideoAsync(
                audioOnlyStreamInfo,
                videoOnlyStreamInfo,
                videoMetadata.Title,
                tempDirectory);

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(
                videoLocalFile,
                tempDirectory,
                GetSupportedHeightResolutions(importerSettings),
                importerSettings.IncludeAudioTrack);

            // Get thumbnail.
            List<ThumbnailLocalFile> thumbnailFiles = new();
            if (videoMetadata.Thumbnail is not null)
            {
                var betsResolutionThumbnail = await DownloadThumbnailAsync(
                    videoMetadata.Thumbnail,
                    videoMetadata.Title,
                    tempDirectory);

                thumbnailFiles = await DownscaleThumbnailAsync(betsResolutionThumbnail, tempDirectory);
            }

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        // Helpers.
        private async Task<ThumbnailLocalFile> DownloadThumbnailAsync(
            Thumbnail thumbnail,
            string videoTitle,
            DirectoryInfo importerTempDirectoryInfo)
        {
            if (thumbnail is null)
                throw new ArgumentNullException(nameof(thumbnail));

            string thumbnailFilePath = Path.Combine(importerTempDirectoryInfo.FullName, $"{videoTitle.ToSafeFileName()}_thumb.jpg");

            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some errors during download of thumbnail");

                try
                {
                    using var httpClient = new HttpClient();
                    var stream = await httpClient.GetStreamAsync(thumbnail.Url);
                    using var fileStream = new FileStream(thumbnailFilePath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);

                    Console.WriteLine("Downloaded thumbnail");
                    break;
                }
                catch
                {
                    if (i + 1 < CommonConsts.DownloadMaxRetry)
                    {
                        Console.WriteLine("Failed. Retry...");
                        await Task.Delay(CommonConsts.DownloadTimespanRetry);
                    }
                }
            }

            return new ThumbnailLocalFile(
                thumbnailFilePath,
                new FileInfo(thumbnailFilePath).Length,
                thumbnail.Resolution.Height,
                thumbnail.Resolution.Width);
        }

        private async Task<VideoLocalFile> DownloadVideoAsync(
            IAudioStreamInfo audioOnlyStream,
            IVideoStreamInfo videoOnlyStream,
            string videoTitle,
            DirectoryInfo importerTempDirectoryInfo)
        {
            var videoFileName = $"{videoTitle.ToSafeFileName()}_{videoOnlyStream.VideoResolution}.{videoOnlyStream.Container}";
            var videoFilePath = Path.Combine(importerTempDirectoryInfo.FullName, videoFileName);
            var videoQualityLabel = videoOnlyStream.VideoQuality.Label;

            // Download video.
            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some errors during download of video");

                try
                {
                    var downloadStart = DateTime.UtcNow;
                    await youtubeClient.Videos.DownloadAsync(
                        new IStreamInfo[] { audioOnlyStream, videoOnlyStream },
                        new ConversionRequestBuilder(videoFilePath).SetFFmpegPath(encoderService.FFMpegBinaryPath).Build(),
                        new Progress<double>((progressStatus) =>
                            PrintProgressLine(
                                $"Downloading and mux {videoQualityLabel}",
                                progressStatus,
                                videoOnlyStream.Size.MegaBytes + audioOnlyStream.Size.MegaBytes,
                                downloadStart)));
                    Console.WriteLine();
                    break;
                }
                catch
                {
                    Console.WriteLine();
                    if (i + 1 < CommonConsts.DownloadMaxRetry)
                    {
                        Console.Write("Failed. Retry...\r");
                        await Task.Delay(CommonConsts.DownloadTimespanRetry);
                    }
                }
            }

            return new VideoLocalFile(
                videoFilePath,
                videoOnlyStream.VideoResolution.Height,
                videoOnlyStream.VideoResolution.Width,
                new FileInfo(videoFilePath).Length);
        }

        private async Task<List<ThumbnailLocalFile>> DownscaleThumbnailAsync(
            ThumbnailLocalFile betsResolutionThumbnail,
            DirectoryInfo importerTempDirectoryInfo)
        {
            List<ThumbnailLocalFile> thumbnails = new();

            using var thumbFileStream = File.OpenRead(betsResolutionThumbnail.FilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailLocalFile.ThumbnailResponsiveSizes)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / betsResolutionThumbnail.AspectRatio);

                using SKBitmap scaledBitmap = thumbBitmap.Resize(new SKImageInfo(responsiveWidthSize, responsiveHeightSize), SKFilterQuality.Medium);
                using SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);
                using SKData data = scaledImage.Encode();

                var thumbnailResizedPath = Path.Combine(importerTempDirectoryInfo.FullName, $"thumb_{responsiveWidthSize}_{responsiveHeightSize}_{Guid.NewGuid()}.jpg");
                using FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew);
                await data.AsStream().CopyToAsync(outputFileStream);

                await outputFileStream.DisposeAsync();
                data.Dispose();
                scaledImage.Dispose();
                scaledBitmap.Dispose();

                thumbnails.Add(new ThumbnailLocalFile(
                    thumbnailResizedPath,
                    new FileInfo(thumbnailResizedPath).Length,
                    responsiveHeightSize,
                    responsiveWidthSize));
            }

            return thumbnails;
        }

        private static IEnumerable<int> GetSupportedHeightResolutions(ImporterSettings importerSettings)
        {
            var supportedHeightResolutions = new List<int>();
            if (!importerSettings.Skip1440)
                supportedHeightResolutions.Add(1440);
            if (!importerSettings.Skip1080)
                supportedHeightResolutions.Add(1080);
            if (!importerSettings.Skip720)
                supportedHeightResolutions.Add(720);
            if (!importerSettings.Skip480)
                supportedHeightResolutions.Add(480);
            if (!importerSettings.Skip360)
                supportedHeightResolutions.Add(360);

            return supportedHeightResolutions;
        }

        private static void PrintProgressLine(string message, double progressStatus, double totalSizeMB, DateTime startDateTime)
        {
            // Calculate ETA.
            var elapsedTime = DateTime.UtcNow - startDateTime;
            TimeSpan? eta = null;
            if (progressStatus != 0)
            {
                var totalRequiredTime = TimeSpan.FromSeconds(elapsedTime.TotalSeconds / progressStatus);
                eta = totalRequiredTime - elapsedTime;
            }

            // Print update.
            var strBuilder = new StringBuilder();

            strBuilder.Append(CultureInfo.InvariantCulture,
                $"{message} ({(progressStatus * 100):N2}%) {progressStatus * totalSizeMB:N2}MB of {totalSizeMB:N2}MB.");

            if (eta is not null)
                strBuilder.Append(CultureInfo.InvariantCulture, $" ETA: {eta:hh\\:mm\\:ss}");

            strBuilder.Append('\r');

            Console.Write(strBuilder.ToString());
        }
    }
}
