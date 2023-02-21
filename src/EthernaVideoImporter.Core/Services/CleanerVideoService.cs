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

using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly IUserIndexClient ethernaIndexClient;

        // Constractor.
        public CleanerVideoService(
            IUserIndexClient ethernaIndexClient)
        {
            this.ethernaIndexClient = ethernaIndexClient;
        }

        [SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "Temporary. Remove with next refactoring")]
        public async Task RunCleanerAsync(
            IEnumerable<VideoMetadataBase> allVideosMetadata,
            IEnumerable<IndexedVideo> importedVideos)
        {
            Console.WriteLine($"Start cleaner invalid video");

            // Get indexed videos.
            var videoIds = importedVideos.Select(
                    videoData => videoData?.LastValidManifest?.PersonalData?.VideoId)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            // Get video indexed but not in repository files.
            var removableIds = videoIds.Except(allVideosMetadata.Select(repVideo => repVideo.Id).ToList());
            foreach (var videoId in removableIds)
            {
                try
                {
                    var itemToRemove = importedVideos
                        .Where(videoData => videoData?.LastValidManifest?.PersonalData?.VideoId == videoId)
                        .First();

                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(itemToRemove.IndexId).ConfigureAwait(false);

                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video {itemToRemove.IndexId} removed");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {videoId}\n");
                    Console.ResetColor();
                }
            }
        }

        public async Task RunOldDeleterAsync(IEnumerable<IndexedVideo> videos)
        {
            if (videos is null)
                throw new ArgumentNullException(nameof(videos));

            Console.WriteLine($"Start deleter old video");

            foreach (var video in videos)
            {
                if (video.LastValidManifest?.PersonalData is not null)
                    continue;

                try
                {
                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(video.IndexId).ConfigureAwait(false);
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video {video.IndexId} removed");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {video.IndexId}\n");
                    Console.ResetColor();
                }
            }
        }
    }
}
