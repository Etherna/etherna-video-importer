//   Copyright 2022-present Etherna Sagl
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
using Etherna.VideoImporter.Core.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Devcon.Services
{
    public sealed class DevconLinkReporterService : ILinkReporterService
    {
        // Fields.
        private const string EthernaIndexPrefix = "ethernaIndex:";
        private const string EthernaPermalinkPrefix = "ethernaPermalink:";
        private readonly ImporterSettings importerSettings;

        public DevconLinkReporterService(IOptions<ImporterSettings> importerSettingsOption)
        {
            if (importerSettingsOption is null)
                throw new ArgumentNullException(nameof(importerSettingsOption));

            this.importerSettings = importerSettingsOption.Value;
        }

        // Methods.
        public async Task SetEthernaReferencesAsync(
            string sourceVideoId,
            string ethernaIndexId,
            string ethernaPermalinkHash)
        {
            var filePath = Path.Combine(importerSettings.SourceUri, sourceVideoId);
            var ethernaIndexUrl = CommonConsts.EthernaIndexContentUrlPrefix + ethernaIndexId;
            var ethernaPermalinkUrl = CommonConsts.EthernaPermalinkContentUrlPrefix + ethernaPermalinkHash;

            // Reaad all line.
            var lines = File.ReadLines(filePath).ToList();

            // Set ethernaIndex.
            var index = GetLineNumber(lines, EthernaIndexPrefix);
            var ethernaIndexLine = $"{EthernaIndexPrefix} \"{ethernaIndexUrl}\"";
            if (index >= 0)
                lines[index] = ethernaIndexLine;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaIndexLine);

            // Set ethernaPermalink.
            index = GetLineNumber(lines, EthernaPermalinkPrefix);
            var ethernaPermalinkLine = $"{EthernaPermalinkPrefix} \"{ethernaPermalinkUrl}\"";
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
            // Last position. (Exclueded final ---)
            return lines - 2;
        }
    }
}
