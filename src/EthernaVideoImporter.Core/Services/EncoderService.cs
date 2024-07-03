﻿// Copyright 2022-present Etherna SA
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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncoderService : IEncoderService
    {
        // Consts.
        private readonly IEnumerable<int> SupportedHeightResolutions = new[] { 360, 480, 720, 1080, 1440 };

        // Fields.
        private readonly IFFmpegService ffMpegService;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IIoService ioService;

        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory,
            IIoService ioService,
            IOptions<EncoderServiceOptions> options)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
            this.httpClientFactory = httpClientFactory;
            this.ioService = ioService;
        }

        // Methods.
        public async Task<IEnumerable<VideoSourceFile>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile)
        {
            ArgumentNullException.ThrowIfNull(sourceVideoFile, nameof(sourceVideoFile));

            var videoEncodedFiles = new List<VideoSourceFile>();
            var outputs = await ffMpegService.EncodeVideosAsync(
                sourceVideoFile,
                SupportedHeightResolutions.Union(new List<int> { sourceVideoFile.Height })
                                          .OrderDescending());

            foreach (var (outputFilePath, outputHeight, outputWidth) in outputs)
            {
                var outputFileSize = new FileInfo(outputFilePath).Length;
                videoEncodedFiles.Add(await VideoSourceFile.BuildNewAsync(
                    new SourceUri(outputFilePath, SourceUriKind.Local),
                    ffMpegService,
                    httpClientFactory));

                ioService.WriteLine($"Encoded output stream {outputHeight}:{outputWidth}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            await RemoveUnusefulResolutionsAsync(videoEncodedFiles);

            ioService.WriteLine($"Keep [{videoEncodedFiles.Select(vf => vf.Height.ToString(CultureInfo.InvariantCulture))
                                                        .Aggregate((r, h) => $"{r}, {h}")}] as valid resolutions to upload");

            return videoEncodedFiles;
        }

        // Helpers.
        private static async Task RemoveUnusefulResolutionsAsync(List<VideoSourceFile> videoFiles)
        {
            var videoFilesWithByteSize = new List<(VideoSourceFile video, long byteSize)>();
            foreach (var file in videoFiles)
                videoFilesWithByteSize.Add((file, await file.GetByteSizeAsync()));

            videoFilesWithByteSize.RemoveAll(
                vf1 => videoFilesWithByteSize.Any(
                    vf2 => vf1.video.Height < vf2.video.Height &&
                           vf1.byteSize >= vf2.byteSize));

            videoFiles.Clear();
            videoFiles.AddRange(videoFilesWithByteSize.Select(p => p.video));
        }
    }
}
