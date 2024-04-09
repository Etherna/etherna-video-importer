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
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using Video = Etherna.VideoImporter.Core.Models.Domain.Video;

namespace Etherna.VideoImporter.Services
{
    internal sealed class YouTubeVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly YouTubeVideoProviderOptions options;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeVideoProvider(
            IOptions<YouTubeVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            this.options = options.Value;
            this.youtubeDownloader = youtubeDownloader;
        }

        // Properties.
        public string SourceName => "YouTube";

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as YouTubeVideoMetadata ?? throw new ArgumentException($"Metadata must be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)));

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            List<YouTubeVideoMetadata> videosMetadata = new();
            foreach (var sourceUrl in options.SourceUrls)
            {
                //channel
                if (ChannelHandle.TryParse(sourceUrl) != null)
                {
                    var youtubeChannel = await youtubeDownloader.YoutubeClient.Channels.GetByHandleAsync(sourceUrl);
                    var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

                    Console.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl}");

                    videosMetadata.AddRange(youtubeVideos.Select(v => new YouTubeVideoMetadata(youtubeDownloader, v.Url)));
                }
                else if (ChannelId.TryParse(sourceUrl) != null)
                {
                    var youtubeChannel = await youtubeDownloader.YoutubeClient.Channels.GetAsync(sourceUrl);
                    var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

                    Console.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl}");

                    videosMetadata.AddRange(youtubeVideos.Select(v => new YouTubeVideoMetadata(youtubeDownloader, v.Url)));
                }
                else if (ChannelSlug.TryParse(sourceUrl) != null)
                {
                    var youtubeChannel = await youtubeDownloader.YoutubeClient.Channels.GetBySlugAsync(sourceUrl);
                    var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

                    Console.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl}");

                    videosMetadata.AddRange(youtubeVideos.Select(v => new YouTubeVideoMetadata(youtubeDownloader, v.Url)));
                }
                else if (UserName.TryParse(sourceUrl) != null)
                {
                    var youtubeChannel = await youtubeDownloader.YoutubeClient.Channels.GetByUserAsync(sourceUrl);
                    var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

                    Console.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl}");

                    videosMetadata.AddRange(youtubeVideos.Select(v => new YouTubeVideoMetadata(youtubeDownloader, v.Url)));
                }
                
                //single video
                else if (VideoId.TryParse(sourceUrl) != null)
                {
                    Console.WriteLine($"Found video on {sourceUrl}");

                    videosMetadata.Add(new YouTubeVideoMetadata(youtubeDownloader, sourceUrl));
                }
                
                else
                    throw new ArgumentException($"Can't parse YouTube url {sourceUrl}");
            }
            
            //remove duplicates by video Id
            var distinctMetadata = videosMetadata.DistinctBy(m => m.Id).ToArray();
            
            Console.WriteLine($"Found {distinctMetadata.Length} distinct videos");

            return distinctMetadata;
        }

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;
    }
}
