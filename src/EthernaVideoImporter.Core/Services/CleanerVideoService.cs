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

using Etherna.ServicesClient.Clients.Gateway;
using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly IUserGatewayClient ethernaGatewayClient;
        private readonly IUserIndexClient ethernaIndexClient;

        // Constructor.
        public CleanerVideoService(
            IUserGatewayClient ethernaGatewayClient,
            IUserIndexClient ethernaIndexClient)
        {
            this.ethernaGatewayClient = ethernaGatewayClient;
            this.ethernaIndexClient = ethernaIndexClient;
        }

        // Methods.
        public async Task<int> DeleteExogenousVideosAsync(
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            if (indexedVideos is null)
                throw new ArgumentNullException(nameof(indexedVideos));

            Console.WriteLine($"Start removing videos not generated with this tool");

            var exogenousVideos = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName != CommonConsts.ImporterIdentifier).ToList();

            foreach (var video in exogenousVideos)
                await RemoveFromIndexAsync(video, gatewayPinnedHashes, unpinRemovedVideos);

            return exogenousVideos.Count;
        }

        public async Task<int> DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            if (indexedVideos is null)
                throw new ArgumentNullException(nameof(indexedVideos));

            Console.WriteLine($"Start removing videos deleted from source");

            var indexedVideosFromImporter = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName == CommonConsts.ImporterIdentifier);
            var sourceRemovedVideos = indexedVideos.Where(
                v => !videosMetadataFromSource.Any(metadata => metadata.Id == v.LastValidManifest!.PersonalData!.VideoId)).ToList();

            foreach (var sourceRemovedVideo in sourceRemovedVideos)
                await RemoveFromIndexAsync(sourceRemovedVideo, gatewayPinnedHashes, unpinRemovedVideos);

            return sourceRemovedVideos.Count;
        }

        // Helpers.
        private async Task RemoveFromIndexAsync(
            IndexedVideo indexedVideo,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            // Remove video.
            bool removeSucceeded = false;
            try
            {
                await ethernaIndexClient.VideosClient.VideosDeleteAsync(indexedVideo.IndexId);
                removeSucceeded = true;

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Video with index Id {indexedVideo} removed");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Unable to remove video with index Id {indexedVideo}");
            }

            Console.ResetColor();

            // Unpin contents.
            if (removeSucceeded &&
                unpinRemovedVideos &&
                indexedVideo.LastValidManifest is not null)
            {
                //videos
                foreach (var streamSource in indexedVideo.LastValidManifest.Sources)
                    await UnpinContentAsync(streamSource.Reference, gatewayPinnedHashes!);

                //thumbnail
                foreach (var thumbSource in indexedVideo.LastValidManifest.Thumbnail.Sources)
                    await UnpinContentAsync(thumbSource.Value, gatewayPinnedHashes!);

                //manifest
                await UnpinContentAsync(indexedVideo.LastValidManifest.Hash, gatewayPinnedHashes!);
            }
        }

        private async Task<bool> UnpinContentAsync(string hash, IEnumerable<string> gatewayPinnedHashes)
        {
            if (!gatewayPinnedHashes.Contains(hash))
                return false;

            try
            {
                await ethernaGatewayClient.ResourcesClient.PinDeleteAsync(hash);

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Resource with hash {hash} unpinned from gateway");

                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Unable to unpin resource with hash {hash} from gateway");

                return false;
            }
        }
    }
}
