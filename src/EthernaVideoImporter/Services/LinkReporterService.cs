﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    internal sealed class LinkReporterService : ILinkReporterService
    {
        // Fields.
        private const string EthernaIndexPrefix = "ethernaIndex:";
        private const string EthernaPermalinkPrefix = "ethernaPermalink:";

        private readonly string mdFilePath;

        // Constructors.
        public LinkReporterService(string mdFilePath)
        {
            if (string.IsNullOrWhiteSpace(mdFilePath))
                throw new ArgumentNullException(nameof(mdFilePath));

            this.mdFilePath = mdFilePath;
        }

        // Methods.
        public async Task SetEthernaFieldsAsync(
            string ethernaIndex,
            string ethernaPermalink)
        {
            // Reaad all line.
            var lines = File.ReadLines(mdFilePath).ToList();


            //TODO check number o fline (min of 2 throw error)

            // Set ethernaIndex.
            var index = GetLineNumber(lines, EthernaIndexPrefix);
            var ethernaIndexLine = $"{EthernaIndexPrefix} \"{ethernaIndex}\"";
            if (index >= 0)
                lines[index] = ethernaIndexLine;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaIndexLine);

            // Set ethernaPermalink.
            index = GetLineNumber(lines, EthernaPermalinkPrefix);
            var ethernaPermalinkLine = $"{EthernaPermalinkPrefix} \"{ethernaPermalink}\"";
            if (index >= 0)
                lines[index] = ethernaPermalinkLine;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaPermalinkLine);

            // Save file.
            await File.WriteAllLinesAsync(mdFilePath, lines).ConfigureAwait(false);
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