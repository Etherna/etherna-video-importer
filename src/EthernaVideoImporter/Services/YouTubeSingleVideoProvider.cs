﻿//   Copyright 2022-present Etherna Sagl
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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Models.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeSingleVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly string videoUrl;
        private readonly bool includeAudioTrack;
        private readonly IEnumerable<int> supportedHeightResolutions;
        private readonly YoutubeClient youtubeClient;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeSingleVideoProvider(
            string videoUrl,
            IEncoderService encoderService,
            bool includeAudioTrack,
            IEnumerable<int> supportedHeightResolutions)
        {
            this.videoUrl = videoUrl;
            this.includeAudioTrack = includeAudioTrack;
            this.supportedHeightResolutions = supportedHeightResolutions;
            youtubeClient = new();
            youtubeDownloader = new YoutubeDownloader(encoderService, youtubeClient);
        }

        // Properties.
        public string SourceName => videoUrl;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata, DirectoryInfo tempDirectory) => youtubeDownloader.GetVideoAsync(
            videoMetadata as YouTubeVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)),
            tempDirectory,
            includeAudioTrack,
            supportedHeightResolutions);

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var metadata = await youtubeClient.Videos.GetAsync(videoUrl);
            var bestStreamInfo = (await youtubeClient.Videos.Streams.GetManifestAsync(metadata.Id))
                .GetVideoOnlyStreams()
                .OrderByDescending(s => s.VideoResolution.Area)
                .First();

            return new[]
            {
                new YouTubeVideoMetadata(
                    metadata.Title,
                    metadata.Description,
                    metadata.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                    bestStreamInfo.VideoQuality.Label,
                    metadata.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                    metadata.Url)
            };
        }
    }
}
