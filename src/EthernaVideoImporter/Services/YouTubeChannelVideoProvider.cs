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
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

namespace Etherna.VideoImporter.Services
{
    internal sealed class YouTubeChannelVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly YouTubeChannelVideoProviderOptions options;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public YouTubeChannelVideoProvider(
            IOptions<YouTubeChannelVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            this.options = options.Value;
            this.youtubeDownloader = youtubeDownloader;
        }

        // Properties.
        public string SourceName => options.ChannelUrl;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as YouTubeVideoMetadata ?? throw new ArgumentException($"Metadata must be of type {nameof(YouTubeVideoMetadata)}", nameof(videoMetadata)),
            options.GenerateThumbnailWhenMissing);

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var youtubeChannel = await youtubeDownloader.YoutubeClient.Channels.GetByHandleAsync(options.ChannelUrl);
            var youtubeVideos = await youtubeDownloader.YoutubeClient.Channels.GetUploadsAsync(youtubeChannel.Url);

            Console.WriteLine($"Found {youtubeVideos.Count} videos");

            var videosMetadata = new List<VideoMetadataBase>();
            foreach (var video in youtubeVideos)
            {
                try
                {
                    var metadata = await youtubeDownloader.YoutubeClient.Videos.GetAsync(video.Url);
                    var bestStreamInfo = (await youtubeDownloader.YoutubeClient.Videos.Streams.GetManifestAsync(metadata.Id))
                        .GetVideoOnlyStreams()
                        .OrderByDescending(s => s.VideoResolution.Area)
                        .First();

                    videosMetadata.Add(new YouTubeVideoMetadata(
                        metadata.Title,
                        metadata.Description,
                        metadata.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                        bestStreamInfo.VideoQuality.Label,
                        metadata.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                        metadata.Url));

                    Console.WriteLine($"Downloaded metadata for {video.Title}");
                }
                catch (HttpRequestException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error retrieving video: {video.Title}. Try again later");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (TimeoutException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Time out retrieving video: {video.Title}. Try again later");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (VideoUnplayableException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Unplayable video: {video.Title}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (YoutubeExplodeException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Can't read information from YouTube: {video.Title}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadata;
        }

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;
    }
}
