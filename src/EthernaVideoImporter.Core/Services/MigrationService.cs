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
using Etherna.Sdk.Tools.Video.Models;
using Etherna.Sdk.Tools.Video.Services;
using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
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
        IGatewayService gatewayService,
        IHasher hasher,
        IHlsService hlsService,
        IUFileProvider uFileProvider)
        : IMigrationService
    {
        // Methods.
        public OperationType DecideOperation(IndexedVideo alreadyIndexedVideo, VideoMetadataBase sourceMetadata)
        {
            ArgumentNullException.ThrowIfNull(alreadyIndexedVideo, nameof(alreadyIndexedVideo));
            
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
        
        public async Task<ThumbnailFile> DownloadThumbnailFile(SwarmHash manifestHash, SwarmUri thumbSourceUri)
        {
            var thumbnailSwarmFile = uFileProvider.BuildNewUFile(new SwarmUUri(thumbSourceUri));
            var thumbnailLocalFile = await uFileProvider.ToLocalUFileAsync(
                thumbnailSwarmFile,
                allowedUriKinds: UUriKind.Online,
                baseDirectory: manifestHash.ToString());

            var thumbnailHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                thumbSourceUri.ToSwarmAddress(manifestHash));
                    
            return await ThumbnailFile.BuildNewAsync(thumbnailLocalFile, thumbnailHash);
        }

        public async Task<VideoEncodingBase> DownloadVideoEncodingFromManifestAsync(
            SwarmHash manifestHash,
            VideoManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest, nameof(manifest));
            
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
                VideoType.Hls => await DownloadHlsVideoEncodingFromManifestAsync(manifestHash, manifest),
                VideoType.Mp4 => await DownloadMp4VideoEncodingFromManifestAsync(manifestHash, manifest),
                _ => throw new InvalidOperationException()
            };
        }
        
        // Helpers.
        private async Task<HlsVideoEncoding> DownloadHlsVideoEncodingFromManifestAsync(
            SwarmHash manifestHash,
            VideoManifest manifest)
        {
            // Get master file.
            /* With index API v0.3 and HLS, master file is the only source with size 0*/
            var masterFileSource = manifest.VideoSources.Single(
                s => s.Metadata.TotalSourceSize == 0);
            
            var masterFileSwarmAddress = masterFileSource.Uri.ToSwarmAddress(manifestHash);
            var masterFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(new SwarmUUri(masterFileSwarmAddress)));
            masterFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                masterFileSwarmAddress);
            
            // Parse master playlist.
            var masterPlaylist = await hlsService.TryParseHlsMasterPlaylistFromFileAsync(masterFile);
            if (masterPlaylist is null)
                throw new InvalidOperationException("Invalid master playlist");
            
            return await hlsService.ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
                manifest.Duration,
                masterFile,
                masterFileSwarmAddress,
                masterPlaylist);
        }
        
        private async Task<Mp4VideoEncoding> DownloadMp4VideoEncodingFromManifestAsync(
            SwarmHash manifestHash,
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
                videoFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                    videoSource.Uri.ToSwarmAddress(manifestHash));
                
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
