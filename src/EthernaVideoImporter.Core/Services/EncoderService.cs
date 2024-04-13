// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used")]
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory,
            IOptions<EncoderServiceOptions> options)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
            this.httpClientFactory = httpClientFactory;
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

                Console.WriteLine($"Encoded output stream {outputHeight}:{outputWidth}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            await RemoveUnusefulResolutionsAsync(videoEncodedFiles);

            Console.WriteLine($"Keep [{videoEncodedFiles.Select(vf => vf.Height.ToString(CultureInfo.InvariantCulture))
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
