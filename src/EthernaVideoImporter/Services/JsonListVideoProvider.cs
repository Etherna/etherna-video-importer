//   Copyright 2022-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.SourceVideoDtos;
using Etherna.VideoImporter.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class JsonListVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IEncoderService encoderService;
        private readonly IFFmpegService ffMpegService;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly JsonListVideoProviderOptions options;

        // Constructor.
        public JsonListVideoProvider(
            IEncoderService encoderService,
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory,
            IOptions<JsonListVideoProviderOptions> options)
        {
            this.encoderService = encoderService;
            this.ffMpegService = ffMpegService;
            this.httpClientFactory = httpClientFactory;
            this.options = options.Value;
        }

        // Properties.
        public string SourceName => options.JsonMetadataUri.OriginalUri;

        // Methods.
        public async Task<Video> GetVideoAsync(
            VideoMetadataBase videoMetadata)
        {
            var sourceVideoMetadata = videoMetadata as SourceVideoMetadata 
                ?? throw new ArgumentException($"Metadata must be of type {nameof(SourceVideoMetadata)}", nameof(videoMetadata));

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(
                sourceVideoMetadata.SourceVideo);

            // Transcode thumbnail resolutions.
            var thumbnailFiles = sourceVideoMetadata.SourceThumbnail is not null ?
                await sourceVideoMetadata.SourceThumbnail.GetScaledThumbnailsAsync(CommonConsts.TempDirectory) :
                Array.Empty<ThumbnailSourceFile>();

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            // Read json list.
            string jsonData = await new SourceFile(options.JsonMetadataUri, httpClientFactory).ReadAsStringAsync();
            string jsonMetadataDirectoryAbsoluteUri = (options.JsonMetadataUri.TryGetParentDirectoryAsAbsoluteUri() ??
                throw new InvalidOperationException("Must exist a parent directory")).Item1;

            // Parse json video list.
            var jsonVideosMetadataDto = JsonSerializer.Deserialize<List<JsonVideoMetadataDto>>(jsonData) 
                ?? throw new InvalidDataException("Invalid Json metadata");

            var allIdsSet = new HashSet<string>();
            var videosMetadataList = new List<VideoMetadataBase>();
            foreach (var metadataDto in jsonVideosMetadataDto)
            {
                // Check Ids uniqueness.
                if (!allIdsSet.Add(metadataDto.Id))
                    throw new InvalidOperationException($"Duplicate video Id found: {metadataDto.Id}");
                foreach (var oldId in metadataDto.OldIds ?? Array.Empty<string>())
                    if (!allIdsSet.Add(oldId))
                        throw new InvalidOperationException($"Duplicate video Id found: {metadataDto.Id} has an already used old id {oldId}");

                try
                {
                    // Build video.
                    var video = await VideoSourceFile.BuildNewAsync(
                        new SourceUri(metadataDto.VideoFilePath, defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri),
                        ffMpegService,
                        httpClientFactory);

                    // Build thumbnail.
                    var thumbnail = await ThumbnailSourceFile.BuildNewAsync(
                        string.IsNullOrWhiteSpace(metadataDto.ThumbnailFilePath) ?
                            new SourceUri(await ffMpegService.ExtractThumbnailAsync(video), SourceUriKind.LocalAbsolute) :
                            new SourceUri(metadataDto.ThumbnailFilePath, defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri),
                        httpClientFactory);

                    // Add video metadata.
                    videosMetadataList.Add(
                        new SourceVideoMetadata(
                            metadataDto.Id,
                            metadataDto.Title,
                            metadataDto.Description,
                            metadataDto.OldIds,
                            video,
                            thumbnail));

                    Console.WriteLine($"Loaded metadata for {metadataDto.Title}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error importing video Id:{metadataDto.Id}.");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadataList;
        }

        public Task ReportEthernaReferencesAsync(VideoImportResultBase importResult) =>
            Task.CompletedTask;
    }
}
