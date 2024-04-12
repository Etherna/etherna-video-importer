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
            var jsonContent = JsonSerializer.Serialize(results, serializerOptions);
            var filePath = options.OutputFilePath ?? throw new InvalidOperationException("OutputFilePath is not set");
            await File.WriteAllTextAsync(filePath, jsonContent);
        }

        public Task ReportResultAsync(VideoImportResultBase importResult)
        {
            results.Add(new VideoImportResultDto(importResult));
            return Task.CompletedTask;
        }
    }
}