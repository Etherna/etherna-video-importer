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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.ResultDtos;
using Etherna.VideoImporter.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class JsonResultReporterService : IResultReporterService
    {
        // Fields.
        private readonly JsonResultReporterOptions options;
        private readonly List<VideoImportResultDto> results = new();
        private readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

        // Constructor.
        public JsonResultReporterService(
            IOptions<JsonResultReporterOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            this.options = options.Value;
        }

        // Methods.
        public async Task FlushResultOutputAsync()
        {
            if (options.OutputFilePath is null)
                return;
            
            var jsonContent = JsonSerializer.Serialize(results, serializerOptions);
            await File.WriteAllTextAsync(options.OutputFilePath, jsonContent);
        }

        public Task ReportResultAsync(VideoImportResultBase importResult)
        {
            results.Add(new VideoImportResultDto(importResult));
            return Task.CompletedTask;
        }
    }
}