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
using Etherna.VideoImporter.Devcon.Models.JsonDto;
using Etherna.VideoImporter.Devcon.Options;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed class DevconResultReporterService : IResultReporterService
    {
        // Fields.
        private readonly DevconResultReporterOptions options;

        // Constructor.
        public DevconResultReporterService(
            IOptions<DevconResultReporterOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            this.options = options.Value;
        }

        // Methods.
        public Task FlushResultOutputAsync() => Task.CompletedTask;

        public async Task ReportResultAsync(VideoImportResultBase importResult)
        {
            if (options.IsDryRun)
                return;
            
            if (importResult is not VideoImportResultSucceeded succededResult)
                return;
            
            var filePath = Path.Combine(options.ResultFolderPath, succededResult.SourceMetadata.SourceId);

            // Read full json file.
            var jsonString = await File.ReadAllTextAsync(filePath);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var jsonRoot = jsonDoc.RootElement;
            
            // Create the new json object with modified value
            using var outputStream = new MemoryStream();
            using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions
            {
                Indented = true
            });
            writer.WriteStartObject();

            bool isWrote = false;
            bool waitingToWrite = false;
            foreach (var property in jsonRoot.EnumerateObject())
            {
                switch (property.Name)
                {
                    case DevconFileDto.YoutubeIdKey:
                    case DevconFileDto.IpfsHashKey:
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                        waitingToWrite = !isWrote;
                        break;
                    
                    case DevconFileDto.SwarmHashKey:
                        writer.WriteString(DevconFileDto.SwarmHashKey, succededResult.ReferenceHash.ToString());
                        isWrote = true;
                        waitingToWrite = false;
                        break;
                    
                    default:
                        if (waitingToWrite)
                        {
                            writer.WriteString(DevconFileDto.SwarmHashKey, succededResult.ReferenceHash.ToString());
                            isWrote = true;
                            waitingToWrite = false;
                        }
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                        break;
                }
            }
            
            writer.WriteEndObject();
            await writer.FlushAsync();
            outputStream.Position = 0;
            
            // Write back json file.
            jsonString = Encoding.UTF8.GetString(outputStream.ToArray());
            await File.WriteAllTextAsync(filePath, jsonString);
        }
    }
}