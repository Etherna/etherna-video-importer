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
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncodingService : IEncodingService
    {
        // Consts.
        public const VideoType DefaultVideoType = VideoType.Hls;
        public static readonly int[] ThumbnailHeightResolutions = [480, 960, 1280];
        public static readonly int[] VideoHeightResolutions = [360, 480, 720, 1080, 1440, 2160, 4320];

        // Fields.
        private readonly IFFmpegService ffMpegService;
        private readonly IGatewayService gatewayService;
        private readonly IHlsParsingService hlsParsingService;
        private readonly IIoService ioService;
        private readonly IUFileProvider uFileProvider;

        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncodingService(
            IFFmpegService ffMpegService,
            IGatewayService gatewayService,
            IHlsParsingService hlsParsingService,
            IIoService ioService,
            IOptions<EncoderServiceOptions> options,
            IUFileProvider uFileProvider)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
            this.gatewayService = gatewayService;
            this.hlsParsingService = hlsParsingService;
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

        public Task<VideoEncodingBase> EncodeVideoAsync(
            VideoEncodingBase sourceEncoding,
            VideoType outputEncoding = DefaultVideoType) =>
            EncodeVideoAsync(sourceEncoding.Variants.MaxBy(v => v.Height)!, outputEncoding);

        public async Task<VideoEncodingBase> EncodeVideoAsync(
            VideoVariantBase sourceVariant,
            VideoType outputEncoding = DefaultVideoType)
        {
            ArgumentNullException.ThrowIfNull(sourceVariant, nameof(sourceVariant));

            var encodedVideo = await ffMpegService.EncodeVideoAsync(
                sourceVariant,
                VideoHeightResolutions.Union(new List<int> { sourceVariant.Height })
                                      .OrderDescending()
                                      .ToArray(),
                outputEncoding);

            foreach (var variant in encodedVideo.Variants)
                ioService.WriteLine($"Encoded video variant {variant.Width}x{variant.Height}, size: {variant.TotalByteSize} byte");

            return encodedVideo;
        }
        
        public async Task<VideoEncodingBase> ParseVideoEncodingFromSourceUriAsync(
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
                    var masterPlaylist = await hlsParsingService.TryReadHlsMasterPlaylistAsync(mainFile);
                    
                    //if is a master playlist
                    if (masterPlaylist is not null) 
                        return await hlsParsingService.ParseHlsMasterPlaylistAsync(
                            ffProbeResult.Format.Duration,
                            mainFile,
                            swarmAddress,
                            masterPlaylist);
                    
                    //else, this is a single stream playlist
                    var variant = await hlsParsingService.ParseHlsStreamPlaylistAsync(
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
    }
}
