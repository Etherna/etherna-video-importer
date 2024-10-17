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

using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Domain.Directories;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.SourceDtos;
using Etherna.VideoImporter.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class JsonListVideoProvider(
        IEncodingService encodingService,
        IFFmpegService ffMpegService,
        IIoService ioService,
        IOptions<JsonListVideoProviderOptions> options,
        IUFileProvider uFileProvider)
        : IVideoProvider
    {
        // Fields.
        private readonly JsonListVideoProviderOptions options = options.Value;

        // Properties.
        public string SourceName => "json";

        // Methods.
        public async Task<Video> BuildVideoFromMetadataAsync(
            VideoMetadataBase videoMetadata,
            ProjectDirectory projectDirectory)
        {
            var sourceVideoMetadata = videoMetadata as JsonVideoMetadata 
                ?? throw new ArgumentException($"Metadata must be of type {nameof(JsonVideoMetadata)}", nameof(videoMetadata));

            // Transcode video resolutions.
            var encodedVideoFiles = await encodingService.EncodeVideoAsync(
                sourceVideoMetadata.VideoEncoding,
                projectDirectory.EncodedDirectory);

            // Transcode thumbnail resolutions.
            var thumbnailFiles = await encodingService.EncodeThumbnailsAsync(
                sourceVideoMetadata.SourceThumbnail,
                projectDirectory.EncodedDirectory);

            return new Video(
                videoMetadata,
                [],
                thumbnailFiles.ToArray(),
                encodedVideoFiles);
        }

        public async Task<VideoMetadataBase[]> GetVideosMetadataAsync(
            WorkingDirectory workingDirectory)
        {
            // Read json list.
            string jsonData = await uFileProvider.BuildNewUFile(options.JsonMetadataUri).ReadToStringAsync();
            string jsonMetadataDirectoryAbsoluteUri = (options.JsonMetadataUri.TryGetParentDirectoryAsAbsoluteUri() ??
                throw new InvalidOperationException("Must exist a parent directory")).OriginalUri;

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
                    var videoEncoding = await ffMpegService.DecodeVideoEncodingFromUUriAsync(
                        new BasicUUri(
                            metadataDto.VideoFilePath,
                            defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri));

                    // Build thumbnail.
                    ThumbnailFile thumbnail;
                    if (string.IsNullOrWhiteSpace(metadataDto.ThumbnailFilePath))
                    {
                        thumbnail = await ThumbnailFile.BuildNewAsync(
                            uFileProvider.BuildNewUFile(new BasicUUri(
                                await ffMpegService.ExtractThumbnailAsync(
                                    videoEncoding.BestVariant,
                                    workingDirectory.NewProjectDirectory()),
                                UUriKind.LocalAbsolute)));
                    }
                    else
                    {
                        thumbnail = await ThumbnailFile.BuildNewAsync(
                            uFileProvider.BuildNewUFile(new BasicUUri(
                                metadataDto.ThumbnailFilePath,
                                defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri)));
                    }

                    // Add video metadata.
                    videosMetadataList.Add(
                        new JsonVideoMetadata(
                            metadataDto.Id,
                            metadataDto.Title,
                            metadataDto.Description,
                            metadataDto.OldIds,
                            videoEncoding,
                            thumbnail,
                            this));

                    ioService.WriteLine($"Loaded metadata for {metadataDto.Title}");
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Error importing video Id:{metadataDto.Id}.");
                    ioService.PrintException(ex);
                }
            }

            return videosMetadataList.ToArray();
        }
    }
}
