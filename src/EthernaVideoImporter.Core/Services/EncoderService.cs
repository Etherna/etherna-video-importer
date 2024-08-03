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
        private static readonly int[] VideoHeightResolutions = [360, 480, 720, 1080, 1440];

        // Fields.
        private readonly IFFmpegService ffMpegService;
        private readonly IIoService ioService;
        private readonly IUniversalUriProvider universalUriProvider;

        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IFFmpegService ffMpegService,
            IIoService ioService,
            IOptions<EncoderServiceOptions> options,
            IUniversalUriProvider universalUriProvider)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
            this.ioService = ioService;
            this.universalUriProvider = universalUriProvider;
        }

        // Methods.
        public async Task<ThumbnailFile[]> EncodeThumbnailsAsync(
            ThumbnailFile sourceThumbnailFile,
            DirectoryInfo tmpDirectory)
        {
            ArgumentNullException.ThrowIfNull(tmpDirectory, nameof(tmpDirectory));
            ArgumentNullException.ThrowIfNull(sourceThumbnailFile, nameof(sourceThumbnailFile));
            ArgumentNullException.ThrowIfNull(universalUriProvider, nameof(universalUriProvider));

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
                using (SKData data = scaledImage.Encode())
                using (FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew))
                {
                    await data.AsStream().CopyToAsync(outputFileStream);
                }

                thumbnails.Add(await ThumbnailFile.BuildNewAsync(
                    universalUriProvider.GetNewUri(thumbnailResizedPath, UniversalUriKind.Local)));
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
                    universalUriProvider.GetNewUri(outputFilePath, UniversalUriKind.Local)));

                ioService.WriteLine($"Encoded output stream {outputHeight}:{outputWidth}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            RemoveUnusefulResolutions(videoEncodedFiles);

            ioService.WriteLine($"Keep [{videoEncodedFiles.Select(vf => vf.Height.ToString(CultureInfo.InvariantCulture))
                                                        .Aggregate((r, h) => $"{r}, {h}")}] as valid resolutions to upload");

            return videoEncodedFiles.ToArray();
        }

        // Helpers.
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
