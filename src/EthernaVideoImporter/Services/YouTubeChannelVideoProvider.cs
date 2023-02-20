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

using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeChannelVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly string channelUrl;
        private readonly string ffMpegBinaryPath;
        private readonly bool includeAudioTrack;
        private readonly YoutubeClient youtubeClient;
        private readonly YoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeChannelVideoProvider(
            string channelUrl,
            string ffMpegBinaryPath)
        {
            this.channelUrl = channelUrl;
            this.ffMpegBinaryPath = ffMpegBinaryPath;
            includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21

            youtubeClient = new();
            youtubeDownloader = new(youtubeClient);
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

            // Get video streams.
            var encodedFiles = new List<FileBase>();
            foreach (var youtubeStreamInfo in youtubeStreamsInfo)
                encodedFiles.Add(await youtubeDownloader.DownloadVideoStreamAsync(
                    youtubeStreamInfo,
                    videoMetadata.Title).ConfigureAwait(false));

            // Get audio only stream.
            if (includeAudioTrack)
                encodedFiles.Add(await youtubeDownloader.DownloadAudioTrackAsync(
                    youtubeStreamsManifest.GetAudioOnlyStreams().GetWithHighestBitrate(),
                    videoMetadata.Title).ConfigureAwait(false));

            // Get thumbnail.
            ThumbnailFile? thumbnailFile = null;
            if (videoMetadata.Thumbnail is not null)
                thumbnailFile = await youtubeDownloader.DownloadThumbnailAsync(
                    videoMetadata.Thumbnail,
                    videoMetadata.Title).ConfigureAwait(false);

            return new Video(videoMetadata, encodedFiles, thumbnailFile);
        }
    }
}
