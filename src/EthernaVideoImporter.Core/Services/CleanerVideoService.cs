// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService : ICleanerVideoService
    {
        // Fields.
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IGatewayService gatewayService;
        private readonly IIoService ioService;

        // Constructor.
        public CleanerVideoService(
            IEthernaUserIndexClient ethernaIndexClient,
            IGatewayService gatewayService,
            IIoService ioService)
        {
            this.ethernaIndexClient = ethernaIndexClient;
            this.gatewayService = gatewayService;
            this.ioService = ioService;
        }

        // Methods.
        public async Task<int> DeleteExogenousVideosAsync(
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<SwarmHash>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            ArgumentNullException.ThrowIfNull(indexedVideos, nameof(indexedVideos));

            ioService.WriteLine($"Start removing videos not generated with this tool");

            var exogenousVideos = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName != CommonConsts.ImporterIdentifier);

            var deindexedVideos = 0;
            foreach (var video in exogenousVideos)
            {
                try
                {
                    await RemoveFromIndexAsync(video, gatewayPinnedHashes, unpinRemovedVideos);
                    deindexedVideos++;
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Impossible to remove from index video {video.IndexId}");
                    ioService.PrintException(ex);
                }
            }

            return deindexedVideos;
        }

        public async Task<int> DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<SwarmHash>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            ArgumentNullException.ThrowIfNull(indexedVideos, nameof(indexedVideos));

            ioService.WriteLine($"Start removing videos deleted from source");

            var indexedVideosFromImporter = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName == CommonConsts.ImporterIdentifier);
            var sourceRemovedVideos = indexedVideos.Where(
                v => videosMetadataFromSource.All(metadata => ManifestPersonalDataDto.HashVideoId(metadata.Id) != v.LastValidManifest?.PersonalData?.VideoIdHash));

            var deindexedVideos = 0;
            foreach (var sourceRemovedVideo in sourceRemovedVideos)
            {
                try
                {
                    await RemoveFromIndexAsync(sourceRemovedVideo, gatewayPinnedHashes, unpinRemovedVideos);
                    deindexedVideos++;
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Impossible to remove from index video {sourceRemovedVideo.IndexId}");
                    ioService.PrintException(ex);
                }
            }

            return deindexedVideos;
        }

        // Helpers.
        private async Task RemoveFromIndexAsync(
            IndexedVideo indexedVideo,
            IEnumerable<SwarmHash>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            // Remove video.
            bool removeSucceeded = false;
            try
            {
                await ethernaIndexClient.OwnerRemoveVideoAsync(indexedVideo.IndexId);
                removeSucceeded = true;

                ioService.WriteSuccessLine($"Video with index Id {indexedVideo.IndexId} removed");
            }
            catch (Exception ex)
            {
                ioService.WriteErrorLine($"Unable to remove video with index Id {indexedVideo.IndexId}");
                ioService.PrintException(ex);
            }

            // Unpin contents.
            if (removeSucceeded &&
                unpinRemovedVideos &&
                indexedVideo.LastValidManifest is not null)
            {
                //videos
                foreach (var streamSource in indexedVideo.LastValidManifest.Sources)
                    await UnpinContentAsync(streamSource.Address.Hash, gatewayPinnedHashes!);

                //thumbnail
                foreach (var thumbSource in indexedVideo.LastValidManifest.Thumbnail.Sources)
                    await UnpinContentAsync(thumbSource.Address.Hash, gatewayPinnedHashes!);

                //manifest
                await UnpinContentAsync(indexedVideo.LastValidManifest.Hash, gatewayPinnedHashes!);
            }
        }

        private async Task<bool> UnpinContentAsync(SwarmHash hash, IEnumerable<SwarmHash> gatewayPinnedHashes)
        {
            if (!gatewayPinnedHashes.Contains(hash))
                return false;

            try
            {
                await gatewayService.DefundResourcePinningAsync(hash);

                ioService.WriteSuccessLine($"Resource with hash {hash} unpinned from gateway");
                
                return true;
            }
            catch (Exception ex)
            {
                ioService.WriteErrorLine($"Unable to unpin resource with hash {hash} from gateway");
                ioService.PrintException(ex);

                return false;
            }
        }
    }
}
