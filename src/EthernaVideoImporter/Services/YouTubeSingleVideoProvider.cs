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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class YouTubeSingleVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly YouTubeSingleVideoProviderOptions options;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeSingleVideoProvider(
            IOptions<YouTubeSingleVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            this.options = options.Value;
            this.youtubeDownloader = youtubeDownloader;
        }

        // Properties.
        public string SourceName => options.VideoUrl;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as YouTubeVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)));

        public Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync() =>
            Task.FromResult<IEnumerable<VideoMetadataBase>>(
                [new YouTubeVideoMetadata(youtubeDownloader, options.VideoUrl)]);

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;
    }
}
