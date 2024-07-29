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

using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Models.Domain;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class CleanerVideoService(
        IEthernaUserIndexClient ethernaIndexClient,
        IGatewayService gatewayService,
        IHasher hasher,
        IIoService ioService)
        : ICleanerVideoService
    {
        // Methods.
        public async Task<int> DeleteExogenousVideosAsync(
            IEnumerable<IndexedVideo> indexedVideos,
            bool unpinRemovedVideos)
        {
            ArgumentNullException.ThrowIfNull(indexedVideos, nameof(indexedVideos));

            ioService.WriteLine($"Start removing videos not generated with this tool");

            var exogenousVideos = indexedVideos.Where(v => v.LastValidManifest?.Manifest.PersonalData?.ClientName != CommonConsts.ImporterIdentifier);

            var deindexedVideos = 0;
            foreach (var video in exogenousVideos)
            {
                try
                {
                    await RemoveFromIndexAsync(video, unpinRemovedVideos);
                    deindexedVideos++;
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Impossible to remove from index video {video.Id}");
                    ioService.PrintException(ex);
                }
            }

            return deindexedVideos;
        }

        public async Task<int> DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos,
            bool unpinRemovedVideos,
            string sourceProviderName)
        {
            ArgumentNullException.ThrowIfNull(indexedVideos, nameof(indexedVideos));

            ioService.WriteLine($"Start removing videos deleted from source");

            // From all indexed videos, select only videos where:
            // - Importer was this same client                                            (not remove videos from other importers)
            // - Video source was this same current one                                   (not remove videos from other sources)
            // - The indexed source id is not listed into any current imported source id  (has been removed from source)
            var sourceRemovedVideos = indexedVideos
                .Where(indexedVideo =>
                    indexedVideo.LastValidManifest?.Manifest.PersonalData?.ClientName == CommonConsts.ImporterIdentifier &&
                    indexedVideo.LastValidManifest?.Manifest.PersonalData?.SourceProviderName == sourceProviderName &&
                    !videosMetadataFromSource.Any(sourceMeta => sourceMeta.AllSourceIds
                        .Select(id => hasher.ComputeHash(id).ToHex()) //get hashed version of all source Ids
                        .Contains(indexedVideo.LastValidManifest!.Manifest.PersonalData!.SourceVideoIdHash)));

            var deindexedVideos = 0;
            foreach (var sourceRemovedVideo in sourceRemovedVideos)
            {
                try
                {
                    await RemoveFromIndexAsync(sourceRemovedVideo, unpinRemovedVideos);
                    deindexedVideos++;
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Impossible to remove from index video {sourceRemovedVideo.Id}");
                    ioService.PrintException(ex);
                }
            }

            return deindexedVideos;
        }

        // Helpers.
        private async Task RemoveFromIndexAsync(
            IndexedVideo indexedVideo,
            bool unpinRemovedVideos)
        {
            // Remove video.
            bool removeSucceeded = false;
            try
            {
                await ethernaIndexClient.OwnerRemoveVideoAsync(indexedVideo.Id);
                removeSucceeded = true;

                ioService.WriteSuccessLine($"Video with index Id {indexedVideo.Id} removed");
            }
            catch (Exception ex)
            {
                ioService.WriteErrorLine($"Unable to remove video with index Id {indexedVideo.Id}");
                ioService.PrintException(ex);
            }

            // Unpin contents.
            if (removeSucceeded &&
                unpinRemovedVideos &&
                indexedVideo.LastValidManifest is not null)
            {
                //videos
                foreach (var streamSource in indexedVideo.LastValidManifest.Manifest.VideoSources
                             .Where(s => s.ManifestUri.UriKind == UriKind.Absolute))
                {
                    var sourceAddress = streamSource.ManifestUri.ToSwarmAddress();
                    var sourceHash = await gatewayService.ResolveSwarmAddressToHashAsync(sourceAddress);
                    await TryDefundPinningAsync(sourceHash);
                }

                //thumbnail
                foreach (var thumbSource in indexedVideo.LastValidManifest.Manifest.Thumbnail.Sources
                             .Where(s => s.ManifestUri.UriKind == UriKind.Absolute))
                {
                    var sourceAddress = thumbSource.ManifestUri.ToSwarmAddress();
                    var sourceHash = await gatewayService.ResolveSwarmAddressToHashAsync(sourceAddress);
                    await TryDefundPinningAsync(sourceHash);
                }

                //manifest
                await TryDefundPinningAsync(indexedVideo.LastValidManifest.Hash);
            }
        }

        private async Task TryDefundPinningAsync(SwarmHash hash)
        {
            try
            {
                await gatewayService.DefundResourcePinningAsync(hash);
                ioService.WriteSuccessLine($"Resource with hash {hash} unpinned from gateway");
            }
            catch (Exception ex)
            {
                ioService.WriteErrorLine($"Unable to unpin resource with hash {hash} from gateway");
                ioService.PrintException(ex);
            }
        }
    }
}
