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
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models;
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

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeChannelVideoProvider : IVideoProvider
    {
        // Const.
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly string channelUrl;
        private readonly DirectoryInfo downloadDirectory;
        private readonly string ffMpegBinaryPath;
        private readonly bool includeAudioTrack;
        private readonly YoutubeClient youtubeClient = new();

        // Constructor.
        public YouTubeChannelVideoProvider(
            string channelUrl,
            string ffMpegBinaryPath)
        {
            this.channelUrl = channelUrl;
            downloadDirectory = Directory.CreateTempSubdirectory();
            this.ffMpegBinaryPath = ffMpegBinaryPath;
            includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
        }

        // Properties.
        public string SourceName => channelUrl;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => GetVideoHelperAsync(
            videoMetadata as YouTubeVideoMetadata ??
            throw new ArgumentException($"Metadata bust be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)));

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var youtubeChannel = await youtubeClient.Channels.GetByHandleAsync(channelUrl).ConfigureAwait(false);
            var youtubeVideos = await youtubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

            var videosMetadata = new List<VideoMetadataBase>();
            foreach (var video in youtubeVideos)
            {
                var metadata = await youtubeClient.Videos.GetAsync(video.Url).ConfigureAwait(false);
                videosMetadata.Add(new YouTubeVideoMetadata(metadata));
            }

            return videosMetadata;
        }

        // Helpers.
        private async Task<Video> GetVideoHelperAsync(YouTubeVideoMetadata videoMetadata)
        {
            // Get manifest data.
            var youtubeStreamsManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoMetadata.YoutubeUrl).ConfigureAwait(false);
            var youtubeStreamsInfo = youtubeStreamsManifest.GetMuxedStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .OrderBy(res => res.VideoResolution.Area);

            // Get muxed streams.
            var sourceVideoInfos = new List<EncodedFileBase>();
            foreach (var youtubeStreamInfo in youtubeStreamsInfo)
                sourceVideoInfos.Add(await DownloadVideoAsync(
                    videoMetadata,
                    youtubeStreamInfo,
                    null).ConfigureAwait(false));

            // Take highest quality MP4 video-only stream and highest bitrate audio-only stream
            var streamInfo = youtubeStreamsManifest
                .GetVideoOnlyStreams()
                .Where(stream => stream.Container == Container.Mp4)
                .OrderBy(stream => stream.VideoResolution.Area);
            var bestStreamAudioInfo = youtubeStreamsManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            foreach (var youtubeStreamInfo in streamInfo)
            {
                if (sourceVideoInfos.OfType<EncodedVideoFile>().Any(svi => svi.VideoQualityLabel == youtubeStreamInfo.VideoQuality.Label) ||
                    bestStreamAudioInfo == null)
                    continue;

                var videoDataResolution = await DownloadVideoAsync(
                    videoMetadata,
                    youtubeStreamInfo,
                    bestStreamAudioInfo).ConfigureAwait(false);
                sourceVideoInfos.Add(videoDataResolution);
            }

            if (includeAudioTrack)
            {
                var audioDataTrack = await DownloadAudioTrackAsync(
                    bestStreamAudioInfo,
                    videoMetadata).ConfigureAwait(false);
                sourceVideoInfos.Add(audioDataTrack);
            }

            // Download thumbnail.
            //find thumbnails for specific area
            var url = videoMetadata.Thumbnails
                .FirstOrDefault(video => video.Resolution.Height == _MAX_AVAILABLE_THUMB_HEIGHT_)
                ?.Url;
            url ??= videoMetadata.Thumbnails.OrderByDescending(video => video.Resolution.Area).FirstOrDefault()?.Url;
            string thumbnailPath = default!;

            string filePath = Path.Combine(downloadDirectory.FullName, $"{videoMetadata.Id}_{_MAX_AVAILABLE_THUMB_HEIGHT_}.jpg");
            var i = 0;
            while (i <= MAX_RETRY)
            {
                if (i == MAX_RETRY)
                    throw new InvalidOperationException($"Some error during download of thumbnail {url}");
                try
                {
                    using var httpClient = new HttpClient();
                    var streamGot = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    await streamGot.CopyToAsync(fileStream).ConfigureAwait(false);

                    thumbnailPath = filePath;
                    break;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            }

            return new Video(videoMetadata, sourceVideoInfos, thumbnailPath);
        }

        private async Task<EncodedAudioFile> DownloadAudioTrackAsync(
            IStreamInfo audioStream,
            YouTubeVideoMetadata videoManifest)
        {
            var videoName = $"{videoManifest.Title.ToSafeFileName()}_onlyaudio";
            var filename = $"{videoManifest.Title.ToSafeFileName()}.audio.{audioStream.Container}";
            var downloadedFilePath = Path.Combine(downloadDirectory.FullName, filename);

            if (videoManifest.Duration is null)
                throw new InvalidOperationException("Invalid duration video");

            var i = 0;
            var downloaded = false;
            while (i < MAX_RETRY &&
                    !downloaded)
                try
                {
                    i++;

                    // Download and process them into one file
                    await youtubeClient.Videos.Streams.DownloadAsync(
                        audioStream,
                        downloadedFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading audio track ({(progressStatus * 100):N0}%) {audioStream.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);

                    downloaded = true;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            if (!downloaded)
                throw new InvalidOperationException($"Some error during download of video {audioStream.Url}");

            return new EncodedAudioFile(
                downloadedFilePath,
                audioStream.Size.Bytes,
                (int)videoManifest.Duration.Value.TotalSeconds);
        }

        private async Task<EncodedVideoFile> DownloadVideoAsync(
            YouTubeVideoMetadata videoManifest,
            IVideoStreamInfo youtubeVideoStreamInfo,
            IStreamInfo? youtubeAudioStreamInfo)
        {
            var videoName = $"{videoManifest.Title.ToSafeFileName()}_{youtubeVideoStreamInfo.VideoResolution}";
            var videoFileName = youtubeAudioStreamInfo is null ?
                $"{videoName}.{youtubeVideoStreamInfo.Container}" :
                $"{videoName}.muxed.{youtubeVideoStreamInfo.Container}";
            var downloadedFilePath = Path.Combine(downloadDirectory.FullName, videoFileName);
            var videoQualityLabel = youtubeVideoStreamInfo.VideoQuality.Label;

            if (videoManifest.Duration is null)
                throw new InvalidOperationException("Invalid duration video");

            var i = 0;
            var downloaded = false;
            while (i < MAX_RETRY && !downloaded)
                try
                {
                    i++;

                    // Download and process them into one file
                    if (youtubeAudioStreamInfo is null)
                        await youtubeClient.Videos.Streams.DownloadAsync(
                        youtubeVideoStreamInfo,
                        downloadedFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading resolution {videoQualityLabel} ({(progressStatus * 100):N0}%) {youtubeVideoStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    else
                    {
                        var streamInfos = new IStreamInfo[] { youtubeAudioStreamInfo, youtubeVideoStreamInfo };
                        await youtubeClient.Videos.DownloadAsync(
                        streamInfos,
                        new ConversionRequestBuilder(downloadedFilePath).SetFFmpegPath(ffMpegBinaryPath).Build(),
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading and mux resolution {videoQualityLabel} ({(progressStatus * 100):N0}%) {youtubeVideoStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);
                    }

                    downloaded = true;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            if (!downloaded)
                throw new InvalidOperationException($"Some error during download of video {youtubeVideoStreamInfo.Url}");

            return new EncodedVideoFile(
                downloadedFilePath,
                videoQualityLabel,
                youtubeVideoStreamInfo.Size.Bytes,
                (int)videoManifest.Duration.Value.TotalSeconds);
        }
    }
}
