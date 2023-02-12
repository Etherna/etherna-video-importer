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
    public sealed class YouTubeChannelVideoParserServices : IVideoParseServices
    {
        // Methods.
        public async Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string uri)
        {
            var youtubeClient = new YoutubeClient();

            var channel = await youtubeClient.Channels.GetByHandleAsync(uri).ConfigureAwait(false);
            var youtubeVideos = await youtubeClient.Channels.GetUploadsAsync(channel.Url);

            List<VideoDataMinimalInfo> videos = new();
            foreach (var video in youtubeVideos)
                videos.Add(new VideoDataMinimalInfo
                {
                    Uri = video.Url,
                });

            return videos;
        }

        public async Task<VideoData?> ToVideoDataDtosAsync(string uri)
        {
            var youtube = new YoutubeClient();

            var videoInfo = await youtube.Videos.GetAsync(uri).ConfigureAwait(false);

            return new VideoData
            {
                Id = videoInfo.Id,
                YoutubeUrl = videoInfo.Url,
                Title = videoInfo.Title,
                Description = videoInfo.Description
            };
        }
    }
}
