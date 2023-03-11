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
        private readonly DirectoryInfo downloadDirectory;
        private readonly string ffMpegPath;
        private readonly YoutubeClient youtubeClient;
        public YoutubeDownloader(
            string ffMpegPath,
            YoutubeClient youtubeClient,
            DirectoryInfo downloadDirectory)
        {
            this.downloadDirectory = downloadDirectory;
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
            var youtubeStreamsManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoMetadata.YoutubeId);

            var videoOnlyStreamsInfo = youtubeStreamsManifest.GetVideoOnlyStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .OrderByDescending(stream => stream.VideoResolution.Area)
                .GroupBy(stream => stream.VideoQuality.Label)
                .Select(qualityGroup => qualityGroup.OrderByDescending(s => s.Bitrate.BitsPerSecond)
                                                    .First());
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
                    throw new InvalidOperationException($"Some errors during download of audio track");

                try
                {
                    var downloadStart = DateTime.UtcNow;
                    await youtubeClient.Videos.Streams.DownloadAsync(
                        audioStream,
                        audioFilePath,
                        new Progress<double>((progressStatus) =>
                            PrintProgressLine(
                                "Downloading audio track",
                                progressStatus,
                                audioStream.Size.MegaBytes,
                                downloadStart)));
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
                    throw new InvalidOperationException($"Some errors during download of video");

                try
                {
                    var downloadStart = DateTime.UtcNow;
                    await youtubeClient.Videos.DownloadAsync(
                        new IStreamInfo[] { audioOnlyStream, videoOnlyStream },
                        new ConversionRequestBuilder(videoFilePath).SetFFmpegPath(ffMpegPath).Build(),
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

            return new VideoFile(
                videoFilePath,
                videoQualityLabel,
                new FileInfo(videoFilePath).Length);
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
