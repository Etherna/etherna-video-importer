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
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Devcon.Models.Domain;
using Etherna.VideoImporter.Devcon.Models.JsonDto;
using Etherna.VideoImporter.Devcon.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed class DevconVideoProvider(
        IIoService ioService,
        IOptions<DevconVideoProviderOptions> options,
        IYoutubeDownloader youtubeDownloader)
        : IVideoProvider
    {
        // Fields.
        private readonly DevconVideoProviderOptions options = options.Value;

        // Properties.
        public string SourceName => "devconFolder";

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as DevconFileVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(DevconFileVideoMetadata)}", nameof(videoMetadata)));

        public async Task<VideoMetadataBase[]> GetVideosMetadataAsync()
        {
            var jsonFilesPaths = Directory.GetFiles(options.DevconSourceFolderPath, "*.json", SearchOption.AllDirectories);

            ioService.WriteLine($"Found {jsonFilesPaths.Length} videos");

            var videosMetadata = new List<(DevconFileDto jsonDto, string jsonRelativePath)>();
            foreach (var (jsonFilePath, i) in jsonFilesPaths.Select((f, i) => (f, i)))
            {
                var jsonFileRelativePath = Path.GetRelativePath(options.DevconSourceFolderPath, jsonFilePath);

                ioService.WriteLine($"File #{i + 1} of {jsonFilesPaths.Length}: {jsonFileRelativePath}");

                DevconFileDto videoDataInfoDto;
                try
                {
                    var content = await File.ReadAllTextAsync(jsonFilePath);
                    videoDataInfoDto = JsonSerializer.Deserialize<DevconFileDto>(content) ??
                                       throw new InvalidDataException("Invalid Json metadata");

                    ioService.WriteLine($"\tParsed video with title: {videoDataInfoDto.Title}");
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine($"Error parsing metadata from file \"{jsonFilePath}\"");
                    ioService.PrintException(ex);

                    continue;
                }
                
                videosMetadata.Add((videoDataInfoDto, jsonFileRelativePath));
            }

            return videosMetadata.Select(
                p => new DevconFileVideoMetadata(
                    p.jsonDto.Title,
                    p.jsonDto.Description,
                    p.jsonRelativePath,
                    youtubeDownloader,
                    p.jsonDto.YoutubeId,
                    p.jsonDto.SwarmHash is null ?
                        (SwarmHash?)null :
                        new SwarmHash(p.jsonDto.SwarmHash))).Cast<VideoMetadataBase>().ToArray();
        }
    }
}
