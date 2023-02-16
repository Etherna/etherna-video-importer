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
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeChannelVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly string channelUrl;
        private readonly YoutubeClient youtubeClient = new();

        // Constructor.
        public YouTubeChannelVideoProvider(string channelUrl)
        {
            this.channelUrl = channelUrl;
        }

        // Methods.
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
    }
}
