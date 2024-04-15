// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using Video = Etherna.VideoImporter.Core.Models.Domain.Video;

namespace Etherna.VideoImporter.Services
{
    internal sealed class YouTubeVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IIoService ioService;
        private readonly YouTubeVideoProviderOptions options;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeVideoProvider(
            IIoService ioService,
            IOptions<YouTubeVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            this.ioService = ioService;
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
                    videosMetadata.AddRange(await GetVideosMetadataFromChannelAsync(
                        await youtubeDownloader.YoutubeClient.Channels.GetByHandleAsync(sourceUrl),
                        sourceUrl));
                else if (ChannelId.TryParse(sourceUrl) != null)
                    videosMetadata.AddRange(await GetVideosMetadataFromChannelAsync(
                        await youtubeDownloader.YoutubeClient.Channels.GetAsync(sourceUrl),
                        sourceUrl));
                else if (ChannelSlug.TryParse(sourceUrl) != null)
                    videosMetadata.AddRange(await GetVideosMetadataFromChannelAsync(
                        await youtubeDownloader.YoutubeClient.Channels.GetBySlugAsync(sourceUrl),
                        sourceUrl));
                else if (UserName.TryParse(sourceUrl) != null)
                    videosMetadata.AddRange(await GetVideosMetadataFromChannelAsync(
                        await youtubeDownloader.YoutubeClient.Channels.GetByUserAsync(sourceUrl),
                        sourceUrl));
                
                //playlist
                else if (PlaylistId.TryParse(sourceUrl) != null)
                    videosMetadata.AddRange(await GetVideosMetadataFromPlaylistAsync(
                        await youtubeDownloader.YoutubeClient.Playlists.GetAsync(sourceUrl),
                        sourceUrl));
                
                //single video
                else if (VideoId.TryParse(sourceUrl) != null)
                    videosMetadata.Add(await GetVideoMetadataFromSingleVideoAsync(
                        sourceUrl));
                
                else
                    throw new ArgumentException($"Can't parse YouTube url {sourceUrl}");
            }
            
            //remove duplicates by video Id
            var distinctMetadata = videosMetadata.DistinctBy(m => m.Id).ToArray();
            
            ioService.WriteLine($"Found {distinctMetadata.Length} distinct videos");

            return distinctMetadata;
        }
        
        // Helpers.
        private async Task<IEnumerable<YouTubeVideoMetadata>> GetVideosMetadataFromChannelAsync(
            Channel youtubeChannel,
            string sourceUrl)
        {
            var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);
                
            ioService.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl} channel");
                
            return youtubeVideos.Select(v => new YouTubeVideoMetadata(
                youtubeDownloader,
                v.Url));
        }

        private async Task<IEnumerable<YouTubeVideoMetadata>> GetVideosMetadataFromPlaylistAsync(
            Playlist youtubePlaylist,
            string sourceUrl)
        {
            var youtubeVideos = await youtubeDownloader.YoutubeClient.Playlists.GetVideosAsync(youtubePlaylist.Url);

            ioService.WriteLine($"Found {youtubeVideos.Count} videos on {sourceUrl} playlist");

            return youtubeVideos.Select(v => new YouTubeVideoMetadata(
                youtubeDownloader,
                v.Url,
                playlistName: youtubePlaylist.Title));
        }

        private Task<YouTubeVideoMetadata> GetVideoMetadataFromSingleVideoAsync(
            string sourceUrl)
        {
            ioService.WriteLine($"Found video on {sourceUrl}");

            return Task.FromResult(new YouTubeVideoMetadata(
                youtubeDownloader,
                sourceUrl));
        }
    }
}
