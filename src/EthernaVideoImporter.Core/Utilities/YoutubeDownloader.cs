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
using Etherna.VideoImporter.Core.Models.Cache;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
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
    public sealed class YoutubeDownloader : IYoutubeDownloader
    {
        // Fields.
        private readonly ICacheService cacheService;
        private readonly IEncoderService encoderService;

        // Constructor.
        public YoutubeDownloader(
            ICacheService cacheService,
            IEncoderService encoderService,
            IYoutubeClient youtubeClient)
        {
            this.cacheService = cacheService;
            this.encoderService = encoderService;
            YoutubeClient = youtubeClient;
        }

        // Properties.
        public IYoutubeClient YoutubeClient { get; }

        // Methods.
        public async Task<Video> GetVideoAsync(YouTubeVideoMetadataBase videoMetadata)
        {
            if (videoMetadata is null)
                throw new ArgumentNullException(nameof(videoMetadata));

            // Get tracking.
            var cacheTracking = await cacheService.GetTrackingAsync(videoMetadata.Id);
            cacheTracking ??= new CacheTracking(videoMetadata.Id);

            // Get best video resolution.
            var videoLocalFile = await GetBestVideoAsync(videoMetadata, cacheTracking);

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(videoMetadata.Id, videoLocalFile);

            // Get thumbnail.
            List<ThumbnailLocalFile> thumbnailFiles = await GetThumbnails(videoMetadata, cacheTracking);

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        // Helpers.
        private async Task<ThumbnailLocalFile> DownloadThumbnailAsync(
            Thumbnail thumbnail,
            string videoTitle)
        {
            if (thumbnail is null)
                throw new ArgumentNullException(nameof(thumbnail));

            string thumbnailFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, $"{videoTitle.ToSafeFileName()}_thumb.jpg");

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
            string videoTitle)
        {
            var videoFileName = $"{videoTitle.ToSafeFileName()}_{videoOnlyStream.VideoResolution}.{videoOnlyStream.Container}";
            var videoFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, videoFileName);
            var videoQualityLabel = videoOnlyStream.VideoQuality.Label;

            // Download video.
            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some errors during download of video");

                try
                {
                    var downloadStart = DateTime.UtcNow;
                    await YoutubeClient.Videos.DownloadAsync(
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
            CacheTracking cacheTracking)
        {
            List<ThumbnailLocalFile> thumbnails = new();

            using var thumbFileStream = File.OpenRead(betsResolutionThumbnail.FilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailLocalFile.ThumbnailResponsiveSizes)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / betsResolutionThumbnail.AspectRatio);

                var thumbnailResizedPath = cacheTracking.GetThumbnailFilePath(responsiveHeightSize, responsiveWidthSize);

                ThumbnailLocalFile thumbnailLocalFile;
                if (string.IsNullOrWhiteSpace(thumbnailResizedPath))
                {
                    using SKBitmap scaledBitmap = thumbBitmap.Resize(new SKImageInfo(responsiveWidthSize, responsiveHeightSize), SKFilterQuality.Medium);
                    using SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);
                    using SKData data = scaledImage.Encode();

                    thumbnailResizedPath = Path.Combine(CommonConsts.TempDirectory.FullName, $"Thumbnail_{responsiveWidthSize}_{responsiveHeightSize}.jpg");
                    using FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew);
                    await data.AsStream().CopyToAsync(outputFileStream);

                    await outputFileStream.DisposeAsync();
                    data.Dispose();
                    scaledImage.Dispose();
                    scaledBitmap.Dispose();

                    // Tracking.
                    thumbnailLocalFile = new ThumbnailLocalFile(
                        thumbnailResizedPath,
                        new FileInfo(thumbnailResizedPath).Length,
                        responsiveHeightSize,
                        responsiveWidthSize);
                    cacheTracking.AddEncodedFilePath(thumbnailLocalFile);
                    await cacheService.SaveTrackingAsync(cacheTracking);
                }
                else
                    thumbnailLocalFile = new ThumbnailLocalFile(
                        thumbnailResizedPath,
                        new FileInfo(thumbnailResizedPath).Length,
                        responsiveHeightSize,
                        responsiveWidthSize);

                thumbnails.Add(thumbnailLocalFile);
            }

            return thumbnails;
        }

        private async Task<VideoLocalFile> GetBestVideoAsync(
            YouTubeVideoMetadataBase videoMetadata,
            CacheTracking cacheTracking)
        {
            VideoLocalFile videoLocalFile;
            if (File.Exists(cacheTracking.OriginalVideoFilePath))
            {
                Console.WriteLine($"Take video from cache");

                videoLocalFile = new VideoLocalFile(
                    cacheTracking.OriginalVideoFilePath,
                    cacheTracking.OriginalVideoHeight,
                    cacheTracking.OriginalVideoWidth,
                    new FileInfo(cacheTracking.OriginalVideoFilePath).Length);
            }
            else
            {
                // Get manifest data.
                var youtubeStreamsManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(videoMetadata.YoutubeId);

                var videoOnlyStreamInfo = youtubeStreamsManifest.GetVideoOnlyStreams()
                    .Where(stream => stream.Container == Container.Mp4)
                    .GetWithHighestVideoQuality();

                var audioOnlyStreamInfo = (IAudioStreamInfo)youtubeStreamsManifest.GetAudioOnlyStreams()
                    .GetWithHighestBitrate();

                // Get high resolution video.
                videoLocalFile = await DownloadVideoAsync(
                    audioOnlyStreamInfo,
                    videoOnlyStreamInfo,
                    videoMetadata.Title);

                // Tracking.
                cacheTracking.SaveOriginalVideo(videoLocalFile);
                await cacheService.SaveTrackingAsync(cacheTracking);
            }

            return videoLocalFile;
        }

        private async Task<List<ThumbnailLocalFile>> GetThumbnails(
            YouTubeVideoMetadataBase videoMetadata,
            CacheTracking cacheTracking)
        {
            List<ThumbnailLocalFile> thumbnailFiles = new();
            if (videoMetadata.Thumbnail is not null)
            {
                ThumbnailLocalFile betsResolutionThumbnail;
                if (File.Exists(cacheTracking.OriginalThumbnailFilePath))
                {
                    Console.WriteLine($"Take thumbnail from cache");

                    betsResolutionThumbnail = new ThumbnailLocalFile(
                            cacheTracking.OriginalThumbnailFilePath,
                            new FileInfo(cacheTracking.OriginalThumbnailFilePath).Length,
                            cacheTracking.OriginalThumbnailHeight,
                            cacheTracking.OriginalThumbnailWidth);
                }
                else
                {
                    betsResolutionThumbnail = await DownloadThumbnailAsync(
                                        videoMetadata.Thumbnail,
                                        videoMetadata.Title);

                    cacheTracking.SaveOriginalThumbnail(betsResolutionThumbnail);
                }

                thumbnailFiles = await DownscaleThumbnailAsync(betsResolutionThumbnail, cacheTracking);
            }

            return thumbnailFiles;
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
