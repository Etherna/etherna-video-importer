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
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Devcon.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed class DevconResultReporterService : IResultReporterService
    {
        // Consts.
        private const string EthernaPermalinkPrefix = "ethernaPermalink:";
        
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
            var swarmHash = UrlBuilder.BuildEmbeddedPermalinkUrl(succededResult.ReferenceHash);

            // Read all line.
            var lines = (await File.ReadAllLinesAsync(filePath)).ToList();

            // Set swarm hash.
            var index = GetLineNumber(lines, EthernaPermalinkPrefix);
            var ethernaPermalinkLine = $"{EthernaPermalinkPrefix} \"{swarmHash}\"";
            if (index >= 0)
                lines[index] = ethernaPermalinkLine;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaPermalinkLine);

            // Save file.
            await File.WriteAllLinesAsync(filePath, lines);
        }
        
        // Helpers.
        private int GetLineNumber(List<string> lines, string prefix)
        {
            var lineIndex = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return lineIndex;

                lineIndex++;
            }
            return -1;
        }

        private int GetIndexOfInsertLine(int lines)
        {
            // Last position. (Excluded final ---)
            return lines - 2;
        }
    }
}