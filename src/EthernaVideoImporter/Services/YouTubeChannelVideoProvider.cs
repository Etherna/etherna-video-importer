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
using YoutubeExplode.Exceptions;

namespace Etherna.VideoImporter.Services
{
    public sealed class YouTubeChannelVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly string channelUrl;
        private readonly bool includeAudioTrack;
        private readonly YoutubeClient youtubeClient;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeChannelVideoProvider(
            string channelUrl,
            string ffMpegPath,
            bool includeAudioTrack)
        {
            this.channelUrl = channelUrl;
            this.includeAudioTrack = includeAudioTrack;
            youtubeClient = new();
            youtubeDownloader = new YoutubeDownloader(ffMpegPath, youtubeClient);
        }

        // Properties.
        public string SourceName => channelUrl;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            includeAudioTrack,
            videoMetadata as YouTubeVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)));

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var youtubeChannel = await youtubeClient.Channels.GetByHandleAsync(channelUrl);
            var youtubeVideos = await youtubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

            Console.WriteLine($"Found {youtubeVideos.Count} videos");

            var videosMetadata = new List<VideoMetadataBase>();
            foreach (var video in youtubeVideos)
            {
                try
                {
                    var metadata = await youtubeClient.Videos.GetAsync(video.Url);
                    var bestStreamInfo = (await youtubeClient.Videos.Streams.GetManifestAsync(metadata.Id))
                        .GetVideoOnlyStreams()
                        .OrderByDescending(s => s.VideoResolution.Area)
                        .First();

                    videosMetadata.Add(new YouTubeVideoMetadata(
                        metadata.Description,
                        metadata.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                        bestStreamInfo.VideoQuality.Label,
                        metadata.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                        metadata.Title,
                        metadata.Url));

                    Console.WriteLine($"Downloaded metadata for {video.Title}");
                }
                catch (VideoUnplayableException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Unplayable video: {video.Title}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadata;
        }
    }
}
