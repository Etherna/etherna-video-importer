// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using System;
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
        private readonly IIoService ioService;

        // Constructor.
        public YoutubeDownloader(
            IEncoderService encoderService,
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory,
            IIoService ioService,
            IYoutubeClient youtubeClient)
        {
            this.encoderService = encoderService;
            this.ffMpegService = ffMpegService;
            this.httpClientFactory = httpClientFactory;
            this.ioService = ioService;
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
            var bestResolutionThumbnail = videoMetadata.Thumbnail is null ?
                await ThumbnailSourceFile.BuildNewAsync(
                    new SourceUri(await ffMpegService.ExtractThumbnailAsync(videoLocalFile), SourceUriKind.LocalAbsolute),
                    httpClientFactory) :
                await DownloadThumbnailAsync(videoMetadata.Thumbnail, videoMetadata.Title);

            var thumbnailFiles = await bestResolutionThumbnail.GetScaledThumbnailsAsync(CommonConsts.TempDirectory);
            
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

                    ioService.WriteLine("Downloaded thumbnail");
                    break;
                }
                catch
                {
                    if (i + 1 < CommonConsts.DownloadMaxRetry)
                    {
                        ioService.WriteLine("Failed. Retry...");
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
                        new Progress<double>(progressStatus =>
                            PrintProgressLine(
                                $"Downloading and mux {videoQualityLabel}",
                                progressStatus,
                                videoOnlyStream.Size.MegaBytes + audioOnlyStream.Size.MegaBytes,
                                downloadStart)));
                    ioService.WriteLine(null, false);
                    break;
                }
                catch
                {
                    ioService.WriteLine(null, false);
                    if (i + 1 < CommonConsts.DownloadMaxRetry)
                    {
                        ioService.PrintTimeStamp();
                        ioService.Write("Failed. Retry...\r");
                        await Task.Delay(CommonConsts.DownloadTimespanRetry);
                    }
                }
            }

            return await VideoSourceFile.BuildNewAsync(
                new SourceUri(videoFilePath, SourceUriKind.Local),
                VideoSourceType.Unknown,
                ffMpegService,
                httpClientFactory);
        }

        private void PrintProgressLine(string message, double progressStatus, double totalSizeMB, DateTime startDateTime)
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

            ioService.Write(strBuilder.ToString());
        }
    }
}
