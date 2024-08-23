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
using YoutubeExplode.Videos.ClosedCaptions;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncodingService : IEncodingService
    {
        // Consts.
        public const VideoType DefaultVideoType = VideoType.Hls;
        public const string EncodedSubtitlesSubDirectory = "encoded/subs";
        public const string EncodedThumbSubDirectory = "encoded/thumb";
        public const string EncodedVideoSubDirectory = "encoded/video";
        public static readonly int[] ThumbnailHeightResolutions = [480, 960, 1280];
        public static readonly int[] VideoHeightResolutions = [360, 480, 720, 1080, 1440, 2160, 4320];

        // Fields.
        private readonly IFFmpegService ffMpegService;
        private readonly IIoService ioService;
        private readonly IUFileProvider uFileProvider;

        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncodingService(
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
        public async Task<SubtitleFile[]> EncodeSubtitlesFromSourceVariantAsync(
            VideoVariantBase sourceVariant,
            ClosedCaptionTrackInfo[] subtitleTracks)
        {
            ArgumentNullException.ThrowIfNull(subtitleTracks, nameof(subtitleTracks));

            if (subtitleTracks.Length == 0)
                return [];
            
            var outputDirectory = Path.Combine(CommonConsts.TempDirectory.FullName, EncodedSubtitlesSubDirectory);

            ioService.WriteLine($"Encoding subtitles [{string.Join(", ", subtitleTracks.Select(t => t.Language.Name))}]...");
            
            var encodedSubFiles = await ffMpegService.EncodeSubtitlesFromSourceVariantAsync(
                sourceVariant,
                subtitleTracks,
                outputDirectory);
            
            ioService.WriteLine($"Encoded subtitles");

            return encodedSubFiles;
        }

        public async Task<ThumbnailFile[]> EncodeThumbnailsAsync(
            ThumbnailFile sourceThumbnailFile)
        {
            ArgumentNullException.ThrowIfNull(sourceThumbnailFile, nameof(sourceThumbnailFile));

            List<ThumbnailFile> thumbnails = [];
            var outputDirectory = Path.Combine(CommonConsts.TempDirectory.FullName, EncodedThumbSubDirectory);
            Directory.CreateDirectory(outputDirectory);

            using var thumbFileStream = await sourceThumbnailFile.ReadToStreamAsync();
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailHeightResolutions)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / sourceThumbnailFile.AspectRatio);
                var thumbnailResizedPath = Path.Combine(outputDirectory, $"{responsiveHeightSize}.jpg");

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
            EncodeVideoAsync(sourceEncoding.BestVariant, outputEncoding);

        public async Task<VideoEncodingBase> EncodeVideoAsync(
            VideoVariantBase sourceVariant,
            VideoType outputEncoding = DefaultVideoType)
        {
            ArgumentNullException.ThrowIfNull(sourceVariant, nameof(sourceVariant));
            
            var outputDirectory = Path.Combine(CommonConsts.TempDirectory.FullName, EncodedVideoSubDirectory);

            var encodedVideo = await ffMpegService.EncodeVideoAsync(
                sourceVariant,
                VideoHeightResolutions.OrderDescending()
                                      .ToArray(),
                outputEncoding,
                outputDirectory);

            foreach (var variant in encodedVideo.Variants)
                ioService.WriteLine($"Encoded video variant {variant.Width}x{variant.Height}, size: {variant.TotalByteSize} byte");

            return encodedVideo;
        }
    }
}
