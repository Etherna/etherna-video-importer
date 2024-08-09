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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class ParsingService(
        IFFmpegService ffMpegService,
        IGatewayService gatewayService,
        IUFileProvider uFileProvider)
        : IParsingService
    {
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
                masterFile,
                variants.ToArray());
        }

        public async Task<VideoEncodingBase> ParseVideoEncodingFromIndexedVideoAsync(IndexedVideo indexedVideo)
        {
            VideoEncodingBase videoEncoding;
            
            VideoType? encodingType = null;
            FileBase? masterFile = null;
            List<VideoVariantBase> videoVariants = [];
            foreach (var videoSource in indexedVideo.LastValidManifest!.Manifest.VideoSources)
            {
                switch (videoSource.Metadata.VideoType)
                {
                    case VideoType.Hls:
                        encodingType ??= VideoType.Hls;
                        if (encodingType != VideoType.Hls)
                            throw new InvalidOperationException();
                        break;
                    case VideoType.Mp4:
                        encodingType ??= VideoType.Mp4;
                        if (encodingType != VideoType.Mp4)
                            throw new InvalidOperationException();
                        break;
                    default: throw new InvalidOperationException();
                }
                
                var videoSwarmFile = uFileProvider.BuildNewUFile(new SwarmUUri(videoSource.Uri));
                var videoLocalFile = await uFileProvider.ToLocalUFileAsync(
                    videoSwarmFile,
                    baseDirectory: indexedVideo.LastValidManifest.Hash.ToString());

                var videoFileHash = await gatewayService.ResolveSwarmAddressToHashAsync(
                    videoSource.Uri.ToSwarmAddress(indexedVideo.LastValidManifest.Hash));
                
                var videoSourceFile = await FileBase.BuildFromUFileAsync(videoLocalFile);
                videoSourceFile.SwarmHash = videoFileHash;
                
                // If is the master file, the size is == 0.
                // Instead, if is a variant, the size is > 0.
                if (videoSource.Metadata.TotalSourceSize == 0)
                {
                    if (masterFile != null)
                        throw new InvalidOperationException();
                    masterFile = videoSourceFile;
                }
                else
                {
                    VideoVariantBase variant;
                    switch (encodingType)
                    {
                        case VideoType.Hls:
                            break;
                        case VideoType.Mp4:
                            
                            variant = new SingleFileVideoVariant(
                                videoSourceFile, videoSource.Metadata.)
                            break;
                        default: throw new InvalidOperationException();
                    }

                    videoVariants.Add(variant);
                }
            }
        }

        public async Task<VideoEncodingBase> ParseVideoEncodingFromUUriAsync(
            BasicUUri mainFileUri,
            SwarmAddress? swarmAddress = null)
        {
            ArgumentNullException.ThrowIfNull(ffMpegService, nameof(ffMpegService));
            ArgumentNullException.ThrowIfNull(mainFileUri, nameof(mainFileUri));
            ArgumentNullException.ThrowIfNull(uFileProvider, nameof(uFileProvider));

            var mainFileAbsoluteUri = mainFileUri.ToAbsoluteUri();
            var mainFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(mainFileAbsoluteUri));
            var ffProbeResult = await ffMpegService.GetVideoInfoAsync(mainFileAbsoluteUri.OriginalUri);

            if (swarmAddress is not null)
                mainFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(swarmAddress.Value);
            
            switch (Path.GetExtension(mainFile.FileName).ToLowerInvariant())
            {
                //hls
                case ".m3u8":
                {
                    var masterPlaylist = await TryParseHlsMasterPlaylistFromFileAsync(mainFile);
                    
                    //if is a master playlist
                    if (masterPlaylist is not null) 
                        return await ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
                            ffProbeResult.Format.Duration,
                            mainFile,
                            swarmAddress,
                            masterPlaylist);
                    
                    //else, this is a single stream playlist
                    var variant = await ParseVideoVariantFromHlsStreamPlaylistFileAsync(
                        mainFile,
                        swarmAddress,
                        ffProbeResult.Streams.First(s => s.Height != 0).Height,
                        ffProbeResult.Streams.First(s => s.Height != 0).Width);
                    return new HlsVideoEncoding(
                        ffProbeResult.Format.Duration,
                        null,
                        [variant]);
                }
                
                //mp4
                case ".mp4":
                    return new Mp4VideoEncoding(
                        ffProbeResult.Format.Duration,
                        [
                            new SingleFileVideoVariant(
                                mainFile,
                                ffProbeResult.Streams.First(s => s.Height != 0).Height,
                                ffProbeResult.Streams.First(s => s.Height != 0).Width)
                        ]
                    );
                
                //mpeg dash
                case ".mpd": throw new NotImplementedException();

                //all other encodings from a single file
                default:
                    return new UndefinedVideoEncoding(
                        ffProbeResult.Format.Duration,
                        [
                            new SingleFileVideoVariant(
                                mainFile,
                                ffProbeResult.Streams.First(s => s.Height != 0).Height,
                                ffProbeResult.Streams.First(s => s.Height != 0).Width)
                        ]
                    );
            }
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
    }
}