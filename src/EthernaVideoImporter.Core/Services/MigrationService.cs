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

using Etherna.BeeNet;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using Etherna.Sdk.Tools.UniversalFiles;
using Etherna.Sdk.Tools.Video.Models;
using Etherna.Sdk.Tools.Video.Services;
using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class MigrationService(
        ISwarmClient beeClient,
        Hasher hasher,
        IHlsService hlsService,
        IUFileProvider uFileProvider)
        : IMigrationService
    {
        // Fields.
        private readonly IReadOnlyChunkStore chunkStore = new SwarmClientChunkStore(beeClient);
        
        // Methods.
        public OperationType DecideOperation(IndexedVideo alreadyIndexedVideo, VideoMetadataBase sourceMetadata)
        {
            ArgumentNullException.ThrowIfNull(alreadyIndexedVideo);
            
            // If client version is missing (0.1.x or 0.2.x).
            if (string.IsNullOrWhiteSpace(alreadyIndexedVideo.PersonalData?.ClientVersion))
                return OperationType.ImportAll;

            var version = new Version(alreadyIndexedVideo.PersonalData.ClientVersion);
            return version switch
            {
                { Major: 0, Minor: <= 2 } => OperationType.ImportAll,
                { Major: 0, Minor: 3, Build: <= 10} => OperationType.ImportAll,
                _ => alreadyIndexedVideo.HasEqualMetadata(sourceMetadata, hasher) ?
                    OperationType.Skip : OperationType.UpdateManifest
            };
        }
        
        public async Task<ThumbnailFile> DownloadThumbnailFile(
            SwarmReference manifestReference,
            SwarmUri thumbSourceUri)
        {
            var thumbnailSwarmFile = uFileProvider.BuildNewUFile(new SwarmUUri(thumbSourceUri));
            var thumbnailLocalFile = await uFileProvider.ToLocalUFileAsync(
                thumbnailSwarmFile,
                allowedUriKinds: UUriKind.Online,
                baseDirectory: manifestReference.ToString());

            var thumbnailChunkRef = await SwarmReference.ResolveFromAddressAsync(
                thumbSourceUri.ToSwarmAddress(manifestReference), chunkStore);
            var thumbnailHash = thumbnailChunkRef.Hash;
                    
            return await ThumbnailFile.BuildNewAsync(thumbnailLocalFile, thumbnailHash);
        }

        public async Task<VideoEncodingBase> DownloadVideoEncodingFromManifestAsync(
            SwarmReference manifestReference,
            VideoManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            
            // Define used video encoding.
            VideoType? encodingType = null;
            foreach (var videoSource in manifest.VideoSources)
            {
                switch (videoSource.Metadata.VideoType)
                {
                    case VideoType.Hls:
                        encodingType ??= VideoType.Hls;
                        if (encodingType != VideoType.Hls)
                            throw new InvalidOperationException("Inconsistent encoding");
                        break;
                    case VideoType.Mp4:
                        encodingType ??= VideoType.Mp4;
                        if (encodingType != VideoType.Mp4)
                            throw new InvalidOperationException("Inconsistent encoding");
                        break;
                    default: throw new InvalidOperationException("Unsupported encoding");
                }
            }

            // Parse sources based on used encoding.
            return encodingType switch
            {
                VideoType.Hls => await DownloadHlsVideoEncodingFromManifestAsync(manifestReference, manifest),
                VideoType.Mp4 => await DownloadMp4VideoEncodingFromManifestAsync(manifestReference, manifest),
                _ => throw new InvalidOperationException()
            };
        }
        
        // Helpers.
        private async Task<HlsVideoEncoding> DownloadHlsVideoEncodingFromManifestAsync(
            SwarmReference manifestReference,
            VideoManifest manifest)
        {
            // Get master file.
            /* With index API v0.3 and HLS, master file is the only source with size 0*/
            var masterFileSource = manifest.VideoSources.Single(
                s => s.Metadata.TotalSourceSize == 0);
            
            var masterFileSwarmAddress = masterFileSource.Uri.ToSwarmAddress(manifestReference);
            var masterFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(new SwarmUUri(masterFileSwarmAddress)));
            masterFile.SwarmReference = await SwarmReference.ResolveFromAddressAsync(masterFileSwarmAddress, chunkStore);
            
            // Parse master playlist.
            var masterPlaylist = await hlsService.TryParseHlsMasterPlaylistFromFileAsync(masterFile);
            if (masterPlaylist is null)
                throw new InvalidOperationException("Invalid master playlist");
            
            return await hlsService.ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
                manifest.Duration,
                masterFile,
                masterFileSwarmAddress,
                masterPlaylist,
                chunkStore);
        }
        
        private async Task<Mp4VideoEncoding> DownloadMp4VideoEncodingFromManifestAsync(
            SwarmReference manifestReference,
            VideoManifest manifest)
        {
            List<SingleFileVideoVariant> videoVariants = [];
            foreach (var videoSource in manifest.VideoSources)
            {
                // Parse sizes.
                if (videoSource.Metadata.Quality is null)
                    throw new InvalidOperationException("Quality can't be null here");
                
                var height = int.Parse(videoSource.Metadata.Quality.TrimEnd('p'), CultureInfo.InvariantCulture);
                var width = (int)(height * manifest.AspectRatio);
                
                // Get video source file.
                var videoFile = await FileBase.BuildFromUFileAsync(
                    uFileProvider.BuildNewUFile(new SwarmUUri(videoSource.Uri)));
                videoFile.SwarmReference = await SwarmReference.ResolveFromAddressAsync(
                    videoSource.Uri.ToSwarmAddress(manifestReference), chunkStore);
                
                // Build and add variant.
                videoVariants.Add(
                    new SingleFileVideoVariant(
                        videoFile,
                        height,
                        width));
            }
            
            return new Mp4VideoEncoding(
                manifest.Duration,
                null,
                videoVariants.ToArray());
        }
    }
}
