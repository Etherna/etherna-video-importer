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
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly IUserIndexClient ethernaIndexClient;

        // Constructor.
        public CleanerVideoService(
            IUserIndexClient ethernaIndexClient)
        {
            this.ethernaIndexClient = ethernaIndexClient;
        }

        // Methods.
        public async Task DeleteExogenousVideosAsync(IEnumerable<IndexedVideo> indexedVideos)
        {
            if (indexedVideos is null)
                throw new ArgumentNullException(nameof(indexedVideos));

            Console.WriteLine($"Start removing videos not generated with this tool");

            foreach (var video in indexedVideos)
            {
                if (video.LastValidManifest?.PersonalData?.ClientName == CommonConsts.ImporterIdentifier)
                    continue;

                try
                {
                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(video.IndexId).ConfigureAwait(false);

                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video with index Id {video.IndexId} removed");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Unable to remove video with index Id {video.IndexId}");
                }

                Console.ResetColor();
            }
        }

        public async Task DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos)
        {
            Console.WriteLine($"Start removing videos deleted from source");

            var indexedVideosFromImporter = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName == CommonConsts.ImporterIdentifier);
            var sourceRemovedVideos = indexedVideos.Where(
                v => !videosMetadataFromSource.Any(metadata => metadata.Id == v.LastValidManifest!.PersonalData!.VideoId));

            foreach (var sourceRemovedVideo in sourceRemovedVideos)
            {
                try
                {
                    await ethernaIndexClient.VideosClient.VideosDeleteAsync(sourceRemovedVideo.IndexId).ConfigureAwait(false);

                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"Video with index Id {sourceRemovedVideo.IndexId} removed");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Unable to remove video with index Id {sourceRemovedVideo.IndexId}");
                }

                Console.ResetColor();
            }
        }
    }
}
