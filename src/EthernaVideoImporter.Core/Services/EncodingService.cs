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

using Etherna.Sdk.Tools.Video.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Domain.Directories;
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
    internal sealed class EncodingService(
        IFFmpegService ffMpegService,
        IIoService ioService,
        IOptions<EncoderServiceOptions> options,
        IUFileProvider uFileProvider)
        : IEncodingService
    {
        // Consts.
        public const VideoType DefaultVideoType = VideoType.Hls;
        public static readonly int[] ThumbnailHeightResolutions = [480, 960, 1280];
        public static readonly int[] VideoHeightResolutions = [360, 480, 720, 1080/*, 1440, 2160, 4320*/]; //tmp: see https://etherna.atlassian.net/browse/EVI-223

        // Fields.
        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields")]
        private readonly EncoderServiceOptions options = options.Value;

        // Methods.
        public async Task<SubtitleFile[]> EncodeSubtitlesFromSourceVariantAsync(
            VideoVariantBase sourceVariant,
            ClosedCaptionTrackInfo[] subtitleTracks,
            EncodedDirectory encodedDirectory)
        {
            ArgumentNullException.ThrowIfNull(subtitleTracks, nameof(subtitleTracks));

            if (subtitleTracks.Length == 0)
                return [];

            ioService.WriteLine($"Encoding subtitles [{string.Join(", ", subtitleTracks.Select(t => t.Language.Name))}]...");
            
            var encodedSubFiles = await ffMpegService.EncodeSubtitlesFromSourceVariantAsync(
                sourceVariant,
                subtitleTracks,
                encodedDirectory.SubtitlesDir.CreateDirectory().FullName);
            
            ioService.WriteLine($"Encoded subtitles");

            return encodedSubFiles;
        }

        public async Task<ThumbnailFile[]> EncodeThumbnailsAsync(
            ThumbnailFile sourceThumbnailFile,
            EncodedDirectory encodedDirectory)
        {
            ArgumentNullException.ThrowIfNull(sourceThumbnailFile, nameof(sourceThumbnailFile));

            List<ThumbnailFile> thumbnails = [];
            var outputDirectory = encodedDirectory.ThumbnailDir.CreateDirectory();

            using var thumbFileStream = await sourceThumbnailFile.ReadToStreamAsync();
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailHeightResolutions)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / sourceThumbnailFile.AspectRatio);
                var thumbnailResizedPath = Path.Combine(outputDirectory.FullName, $"{responsiveHeightSize}.jpg");

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
            EncodedDirectory encodedDirectory,
            VideoType outputEncoding = DefaultVideoType) =>
            EncodeVideoAsync(sourceEncoding.BestVariant, encodedDirectory, outputEncoding);

        public async Task<VideoEncodingBase> EncodeVideoAsync(
            VideoVariantBase sourceVariant,
            EncodedDirectory encodedDirectory,
            VideoType outputEncoding = DefaultVideoType)
        {
            ArgumentNullException.ThrowIfNull(sourceVariant, nameof(sourceVariant));

            var encodedVideo = await ffMpegService.EncodeVideoAsync(
                sourceVariant,
                VideoHeightResolutions.OrderDescending()
                                      .ToArray(),
                outputEncoding,
                encodedDirectory.VideoDir.CreateDirectory().FullName);

            foreach (var variant in encodedVideo.Variants)
                ioService.WriteLine($"Encoded video variant {variant.Width}x{variant.Height}, size: {variant.TotalByteSize} byte");

            return encodedVideo;
        }
    }
}
