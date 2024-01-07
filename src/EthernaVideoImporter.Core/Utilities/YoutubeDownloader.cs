//   Copyright 2022-present Etherna SA
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
        private readonly IEncoderService encoderService;
        private readonly IFFmpegService ffMpegService;
        private readonly IHttpClientFactory httpClientFactory;

        // Constructor.
        public YoutubeDownloader(
            IEncoderService encoderService,
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory,
            IYoutubeClient youtubeClient)
        {
            this.encoderService = encoderService;
            this.ffMpegService = ffMpegService;
            this.httpClientFactory = httpClientFactory;
            YoutubeClient = youtubeClient;
        }

        // Properties.
        public IYoutubeClient YoutubeClient { get; }

        // Methods.
        public async Task<Video> GetVideoAsync(YouTubeVideoMetadataBase videoMetadata)
        {
            ArgumentNullException.ThrowIfNull(videoMetadata, nameof(videoMetadata));

            // Get manifest data.
            var youtubeStreamsManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(videoMetadata.YoutubeId);

            var videoOnlyStreamInfo = youtubeStreamsManifest.GetVideoOnlyStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .GetWithHighestVideoQuality();

            var audioOnlyStreamInfo = (IAudioStreamInfo)youtubeStreamsManifest.GetAudioOnlyStreams()
                .GetWithHighestBitrate();

            // Get high resolution video.
            var videoLocalFile = await DownloadVideoAsync(
                audioOnlyStreamInfo,
                videoOnlyStreamInfo,
                videoMetadata.Title);

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(videoLocalFile);

            // Get thumbnail.
            IEnumerable<ThumbnailSourceFile> thumbnailFiles = Array.Empty<ThumbnailSourceFile>();
            if (videoMetadata.Thumbnail is not null)
            {
                var bestResolutionThumbnail = await DownloadThumbnailAsync(
                    videoMetadata.Thumbnail,
                    videoMetadata.Title);

                thumbnailFiles = await bestResolutionThumbnail.GetScaledThumbnailsAsync(CommonConsts.TempDirectory);
            }

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        // Helpers.
        private async Task<ThumbnailSourceFile> DownloadThumbnailAsync(
            Thumbnail thumbnail,
            string videoTitle)
        {
            ArgumentNullException.ThrowIfNull(thumbnail, nameof(thumbnail));

            string thumbnailFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, $"{videoTitle.ToSafeFileName()}_thumb.jpg");

            for (int i = 0; i <= CommonConsts.DownloadMaxRetry; i++)
            {
                if (i == CommonConsts.DownloadMaxRetry)
                    throw new InvalidOperationException($"Some errors during download of thumbnail");

                try
                {
                    using var httpClient = httpClientFactory.CreateClient();
                    using var stream = await httpClient.GetStreamAsync(thumbnail.Url);
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

            return await ThumbnailSourceFile.BuildNewAsync(
                new SourceUri(thumbnailFilePath, SourceUriKind.Local),
                httpClientFactory);
        }

        private async Task<VideoSourceFile> DownloadVideoAsync(
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
                        new ConversionRequestBuilder(videoFilePath).SetFFmpegPath(await ffMpegService.GetFFmpegBinaryPathAsync()).Build(),
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

            return await VideoSourceFile.BuildNewAsync(
                new SourceUri(videoFilePath, SourceUriKind.Local),
                ffMpegService,
                httpClientFactory);
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
