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
using Etherna.VideoImporter.Core.Options;
using M3U8Parser;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncoderService : IEncoderService
    {
        // Consts.
        private static readonly int[] ThumbnailHeightResolutions = [480, 960, 1280];
        private static readonly int[] VideoHeightResolutions = [360, 480, 720, 1080, 1440, 2160, 4320];

        // Fields.
        private readonly IFFmpegService ffMpegService;
        private readonly IIoService ioService;
        private readonly IUFileProvider uFileProvider;

        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IFFmpegService ffMpegService,
            IIoService ioService,
            IOptions<EncoderServiceOptions> options,
            IUFileProvider uFileProvider)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
            this.ioService = ioService;
            this.uFileProvider = uFileProvider;
        }

        // Methods.
        public async Task<ThumbnailFile[]> EncodeThumbnailsAsync(
            ThumbnailFile sourceThumbnailFile,
            DirectoryInfo tmpDirectory)
        {
            ArgumentNullException.ThrowIfNull(tmpDirectory, nameof(tmpDirectory));
            ArgumentNullException.ThrowIfNull(sourceThumbnailFile, nameof(sourceThumbnailFile));

            List<ThumbnailFile> thumbnails = [];

            using var thumbFileStream = await sourceThumbnailFile.ReadToStreamAsync();
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailHeightResolutions)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / sourceThumbnailFile.AspectRatio);
                var thumbnailResizedPath = Path.Combine(tmpDirectory.FullName, $"{responsiveHeightSize}.jpg");

                using (SKBitmap scaledBitmap = thumbBitmap.Resize(new SKImageInfo(responsiveWidthSize, responsiveHeightSize), SKFilterQuality.Medium))
                using (SKImage scaledImage = SKImage.FromBitmap(scaledBitmap))
                using (SKData data = scaledImage.Encode(SKEncodedImageFormat.Jpeg, 75))
                using (FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew))
                {
                    await data.AsStream().CopyToAsync(outputFileStream);
                }

                thumbnails.Add(await ThumbnailFile.BuildNewAsync(
                    uFileProvider.BuildNewUFile(new BasicUUri(thumbnailResizedPath, UUriKind.Local))));
            }

            return thumbnails.ToArray();
        }

        public async Task<VideoFile[]> EncodeVideosAsync(
            VideoFile videoFile)
        {
            ArgumentNullException.ThrowIfNull(videoFile, nameof(videoFile));

            var videoEncodedFiles = new List<VideoFile>();
            var outputs = await ffMpegService.EncodeVideosAsync(
                videoFile,
                VideoHeightResolutions.Union(new List<int> { videoFile.Height })
                                      .OrderDescending());

            foreach (var (outputFilePath, outputHeight, outputWidth) in outputs)
            {
                var outputFileSize = new FileInfo(outputFilePath).Length;
                videoEncodedFiles.Add(await VideoFile.BuildNewAsync(
                    ffMpegService,
                    uFileProvider.BuildNewUFile(new BasicUUri(outputFilePath, UUriKind.Local))));

                ioService.WriteLine($"Encoded output stream {outputWidth}x{outputHeight}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            RemoveUnusefulResolutions(videoEncodedFiles);

            ioService.WriteLine($"Keep [{videoEncodedFiles.Select(vf => vf.Height.ToString(CultureInfo.InvariantCulture))
                                                        .Aggregate((r, h) => $"{r}, {h}")}] as valid resolutions to upload");

            return videoEncodedFiles.ToArray();
        }
        
        public async Task<VideoEncodingBase> ParseVideoEncodingFromSourceAsync(
            BasicUUri mainFileUri,
            SwarmAddress? swarmAddress = null)
        {
            ArgumentNullException.ThrowIfNull(ffMpegService, nameof(ffMpegService));
            ArgumentNullException.ThrowIfNull(mainFileUri, nameof(mainFileUri));
            ArgumentNullException.ThrowIfNull(uFileProvider, nameof(uFileProvider));

            var mainFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(mainFileUri));

            if (swarmAddress is not null)
                mainFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(swarmAddress.Value);
            
            switch (Path.GetExtension(mainFile.FileName).ToLowerInvariant())
            {
                //hls
                case ".m3u8":
                {
                    var masterPlaylist = await TryReadHlsMasterPlaylistAsync(mainFile);
                    
                    //if is a master playlist
                    if (masterPlaylist is not null) 
                        return await ParseHlsMasterPlaylistAsync(mainFile, swarmAddress, masterPlaylist);
                    
                    //else, this is a single stream playlist
                    var (absoluteFileUri, _) = mainFile.UUri.ToAbsoluteUri();
                    var ffProbeResult = await ffMpegService.GetVideoInfoAsync(absoluteFileUri);
                    
                    var variant = await ParseHlsStreamPlaylistAsync(
                        mainFile,
                        swarmAddress,
                        ffProbeResult.Streams.First(s => s.Height != 0).Height,
                        ffProbeResult.Streams.First(s => s.Height != 0).Width);
                    return new HlsVideoEncoding(null, [variant]);
                }
                
                //mpeg dash
                case ".mpd": throw new NotImplementedException();

                //all other encodings from a single file
                default:
                {
                    var (absoluteFileUri, _) = mainFile.UUri.ToAbsoluteUri();
                    var ffProbeResult = await ffMpegService.GetVideoInfoAsync(absoluteFileUri);
                    return new UnknownVideoEncoding(
                        [
                            new SingleFileVideoVariant(
                                mainFile,
                                ffProbeResult.Streams.First(s => s.Height != 0).Height,
                                ffProbeResult.Streams.First(s => s.Height != 0).Width)
                        ]
                    );
                }
            }
        }

        // Helpers.
        private async Task<HlsVideoEncoding> ParseHlsMasterPlaylistAsync(
            FileBase masterFile,
            SwarmAddress? masterSwarmAddress,
            MasterPlaylist masterPlaylist)
        {
            var masterFileDirectory = masterFile.UUri.TryGetParentDirectoryAsAbsoluteUri();
            if (masterFileDirectory is null)
                throw new InvalidOperationException($"Can't get parent directory of {masterFile.UUri.OriginalUri}");
                        
            // Build video variants from streams on master playlist.
            List<HlsVideoVariant> variants = [];
            foreach (var streamInfo in masterPlaylist.Streams)
            {
                // Read stream info.
                var streamRelativeUUri = new BasicUUri(streamInfo.Uri, UUriKind.Relative);
                var (streamAbsoluteUri, streamAbsoluteUriKind) = streamRelativeUUri.ToAbsoluteUri(
                    baseDirectory: masterFileDirectory.Value.Item1);

                // Build stream playlist file.
                var streamPlaylistFile = await FileBase.BuildFromUFileAsync(
                    uFileProvider.BuildNewUFile(new BasicUUri(streamAbsoluteUri, streamAbsoluteUriKind)));
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
                var variant = await ParseHlsStreamPlaylistAsync(
                    streamPlaylistFile,
                    streamSwarmAddress,
                    (int)streamInfo.Resolution.Height,
                    (int)streamInfo.Resolution.Width);
                
                variants.Add(variant);
            }
                        
            return new HlsVideoEncoding(
                masterFile,
                variants.ToArray());
        }

        private async Task<HlsVideoVariant> ParseHlsStreamPlaylistAsync(
            FileBase streamPlaylistFile,
            SwarmAddress? streamPlaylistSwarmAddress,
            int height,
            int width)
        {
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
                var (segmentAbsoluteUri, segmentAbsoluteUriKind) = segmentRelativeUUri.ToAbsoluteUri(
                    baseDirectory: streamPlaylistDirectory.Value.Item1);
                    
                // Build segment file.
                var segmentFile = await FileBase.BuildFromUFileAsync(
                    uFileProvider.BuildNewUFile(new BasicUUri(segmentAbsoluteUri, segmentAbsoluteUriKind)));
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
        
        private async Task<MasterPlaylist?> TryReadHlsMasterPlaylistAsync(FileBase hlsPlaylist)
        {
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
        
        private static void RemoveUnusefulResolutions(List<VideoFile> videoFiles)
        {
            var videoFilesWithByteSize = new List<(VideoFile video, long byteSize)>();
            foreach (var file in videoFiles)
                videoFilesWithByteSize.Add((file, file.ByteSize));

            videoFilesWithByteSize.RemoveAll(
                vf1 => videoFilesWithByteSize.Any(
                    vf2 => vf1.video.Height < vf2.video.Height &&
                           vf1.byteSize >= vf2.byteSize));

            videoFiles.Clear();
            videoFiles.AddRange(videoFilesWithByteSize.Select(p => p.video));
        }
    }
}
