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
using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Models.Domain;
using M3U8Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class ParsingService(
        IGatewayService gatewayService,
        IUFileProvider uFileProvider)
        : IParsingService
    {
        public async Task<ThumbnailFile[]> ParseThumbnailFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest)
        {
            ArgumentNullException.ThrowIfNull(publishedManifest, nameof(publishedManifest));
            
            List<ThumbnailFile> thumbnailFiles = [];
            foreach (var thumbnailSource in
                     publishedManifest.Manifest.Thumbnail.Sources)
            {
                var thumbnailSwarmFile = uFileProvider.BuildNewUFile(new SwarmUUri(thumbnailSource.Uri));
                var thumbnailLocalFile = await uFileProvider.ToLocalUFileAsync(
                    thumbnailSwarmFile,
                    baseDirectory: publishedManifest.Hash.ToString());

                var thumbnailHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                    thumbnailSource.Uri.ToSwarmAddress(publishedManifest.Hash));
                    
                thumbnailFiles.Add(await ThumbnailFile.BuildNewAsync(
                    thumbnailLocalFile, thumbnailHash));
            }

            return thumbnailFiles.ToArray();
        }
        
        public async Task<HlsVideoEncoding> ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
            TimeSpan duration,
            FileBase masterFile,
            SwarmAddress? masterSwarmAddress,
            MasterPlaylist masterPlaylist)
        {
            ArgumentNullException.ThrowIfNull(masterFile, nameof(masterFile));
            ArgumentNullException.ThrowIfNull(masterPlaylist, nameof(masterPlaylist));

            // Get master playlist directory.
            var masterFileDirectory = masterFile.UUri.TryGetParentDirectoryAsAbsoluteUri();
            if (masterFileDirectory is null)
                throw new InvalidOperationException($"Can't get parent directory of {masterFile.UUri.OriginalUri}");
                        
            // Build video variants from streams on master playlist.
            List<HlsVideoVariant> variants = [];
            foreach (var streamInfo in masterPlaylist.Streams)
            {
                // Read stream info.
                var streamRelativeUUri = new BasicUUri(streamInfo.Uri, UUriKind.Relative);
                var streamAbsoluteUri = streamRelativeUUri.ToAbsoluteUri(
                    baseDirectory: masterFileDirectory.OriginalUri);

                // Build stream playlist file.
                var streamPlaylistFile = await FileBase.BuildFromUFileAsync(
                    uFileProvider.BuildNewUFile(streamAbsoluteUri));
                SwarmAddress? streamSwarmAddress = null;
                if (masterSwarmAddress is not null)
                {
                    streamSwarmAddress = new SwarmAddress(
                        masterSwarmAddress.Value.Hash,
                        masterSwarmAddress.Value.Path.TrimEnd(SwarmAddress.Separator) + SwarmAddress.Separator + streamInfo.Uri
                    );
                    streamPlaylistFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(streamSwarmAddress.Value);
                }
                
                // Parse stream playlist.
                var variant = await ParseVideoVariantFromHlsStreamPlaylistFileAsync(
                    streamPlaylistFile,
                    streamSwarmAddress,
                    (int)streamInfo.Resolution.Height,
                    (int)streamInfo.Resolution.Width);
                
                variants.Add(variant);
            }
                        
            return new HlsVideoEncoding(
                duration,
                masterFileDirectory.OriginalUri,
                masterFile,
                variants.ToArray());
        }

        public async Task<VideoEncodingBase> ParseVideoEncodingFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest)
        {
            ArgumentNullException.ThrowIfNull(publishedManifest, nameof(publishedManifest));
            
            // Define used video encoding.
            VideoType? encodingType = null;
            foreach (var videoSource in publishedManifest.Manifest.VideoSources)
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
                VideoType.Hls => await ParseHlsVideoEncodingFromPublishedVideoManifestAsync(publishedManifest),
                VideoType.Mp4 => await ParseMp4VideoEncodingFromPublishedVideoManifestAsync(publishedManifest),
                _ => throw new InvalidOperationException()
            };
        }

        public async Task<HlsVideoVariant> ParseVideoVariantFromHlsStreamPlaylistFileAsync(
            FileBase streamPlaylistFile,
            SwarmAddress? streamPlaylistSwarmAddress,
            int height,
            int width)
        {
            ArgumentNullException.ThrowIfNull(streamPlaylistFile, nameof(streamPlaylistFile));
            
            // Get stream playlist directory.
            var streamPlaylistDirectory = streamPlaylistFile.UUri.TryGetParentDirectoryAsAbsoluteUri();
            if (streamPlaylistDirectory is null)
                throw new InvalidOperationException($"Can't get parent directory of {streamPlaylistFile.UUri.OriginalUri}");
                            
            // Parse segments.
            var streamPlaylist = MediaPlaylist.LoadFromText(await streamPlaylistFile.ReadToStringAsync());
            List<FileBase> segmentFiles = [];
            foreach (var segment in streamPlaylist.MediaSegments.First().Segments)
            {
                // Read segments info.
                var segmentRelativeUUri = new BasicUUri(segment.Uri, UUriKind.Relative);
                var segmentAbsoluteUri = segmentRelativeUUri.ToAbsoluteUri(
                    baseDirectory: streamPlaylistDirectory.OriginalUri);
                    
                // Build segment file.
                var segmentFile = await FileBase.BuildFromUFileAsync(uFileProvider.BuildNewUFile(segmentAbsoluteUri));
                if (streamPlaylistSwarmAddress is not null)
                {
                    var segmentSwarmAddress = new SwarmAddress(
                        streamPlaylistSwarmAddress.Value.Hash,
                        streamPlaylistSwarmAddress.Value.Path.TrimEnd(SwarmAddress.Separator) + SwarmAddress.Separator + segment.Uri);
                    segmentFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(segmentSwarmAddress);
                }

                segmentFiles.Add(segmentFile);
            }

            return new HlsVideoVariant(
                streamPlaylistFile,
                segmentFiles.ToArray(),
                height,
                width);
        }
        
        public async Task<MasterPlaylist?> TryParseHlsMasterPlaylistFromFileAsync(FileBase hlsPlaylist)
        {
            ArgumentNullException.ThrowIfNull(hlsPlaylist, nameof(hlsPlaylist));
            
            var hlsPlaylistText = await hlsPlaylist.ReadToStringAsync();
            
            // We need to exclude at first that this is a stream playlist.
            // In fact, parsing a stream playlist as a master playlist gives false positive,
            // but parsing a master playlist as a stream playlist throws exception.
            try
            {
                MediaPlaylist.LoadFromText(hlsPlaylistText); //must throw exception.
                return null;
            }
            catch (NullReferenceException) { }

            return MasterPlaylist.LoadFromText(hlsPlaylistText);
        }
        
        // Helpers.
        private async Task<HlsVideoEncoding> ParseHlsVideoEncodingFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest)
        {
            // Get master file.
            /* With index API v0.3 and HLS, master file is the only source with size 0*/
            var masterFileSource = publishedManifest.Manifest.VideoSources.Single(
                s => s.Metadata.TotalSourceSize == 0);
            
            var masterFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(new SwarmUUri(masterFileSource.Uri)));
            var masterFileSwarmAddress = masterFileSource.Uri.ToSwarmAddress(publishedManifest.Hash);
            masterFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                masterFileSwarmAddress);
            
            // Parse master playlist.
            var masterPlaylist = await TryParseHlsMasterPlaylistFromFileAsync(masterFile);
            if (masterPlaylist is null)
                throw new InvalidOperationException("Invalid master playlist");
            
            return await ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
                publishedManifest.Manifest.Duration,
                masterFile,
                masterFileSwarmAddress,
                masterPlaylist);
        }
        
        private async Task<Mp4VideoEncoding> ParseMp4VideoEncodingFromPublishedVideoManifestAsync(
            PublishedVideoManifest publishedManifest)
        {
            List<SingleFileVideoVariant> videoVariants = [];
            foreach (var videoSource in publishedManifest.Manifest.VideoSources)
            {
                // Parse sizes.
                if (videoSource.Metadata.Quality is null)
                    throw new InvalidOperationException("Quality can't be null here");
                
                var height = int.Parse(videoSource.Metadata.Quality.TrimEnd('p'), CultureInfo.InvariantCulture);
                var width = (int)(height * publishedManifest.Manifest.AspectRatio);
                
                // Get video source file.
                var videoFile = await FileBase.BuildFromUFileAsync(
                    uFileProvider.BuildNewUFile(new SwarmUUri(videoSource.Uri)));
                videoFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                    videoSource.Uri.ToSwarmAddress(publishedManifest.Hash));
                
                // Build and add variant.
                videoVariants.Add(
                    new SingleFileVideoVariant(
                        videoFile,
                        height,
                        width));
            }
            
            return new Mp4VideoEncoding(
                publishedManifest.Manifest.Duration,
                null,
                videoVariants.ToArray());
        }
    }
}