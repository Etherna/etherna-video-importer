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
using Etherna.VideoImporter.Devcon.Models.Domain;
using Etherna.VideoImporter.Devcon.Models.MdDto;
using Etherna.VideoImporter.Devcon.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed partial class MdVideoProvider(
        IIoService ioService,
        IOptions<MdVideoProviderOptions> options,
        IYoutubeDownloader youtubeDownloader)
        : IVideoProvider
    {
        // Consts.
        [GeneratedRegex("(?<!\\\\)\"")]
        private static partial Regex UnescapedQuotesCounterRegex();

        [GeneratedRegex("^\\s*---(?<body>[\\s\\S]+)---\\s*$")]
        private static partial Regex YamlBodyRegex();

        // Fields.
        private readonly IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        private readonly MdVideoProviderOptions options = options.Value;

        // Constructor.

        // Properties.
        public string SourceName => "devconFolder";

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as MdFileVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(MdFileVideoMetadata)}", nameof(videoMetadata)));

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var mdFilesPaths = Directory.GetFiles(options.MdSourceFolderPath, "*.md", SearchOption.AllDirectories);

            ioService.WriteLine($"Found {mdFilesPaths.Length} videos");

            var videosMetadata = new List<(ArchiveMdFileDto mdDto, string mdRelativePath)>();
            foreach (var (mdFilePath, i) in mdFilesPaths.Select((f, i) => (f, i)))
            {
                var mdFileRelativePath = Path.GetRelativePath(options.MdSourceFolderPath, mdFilePath);

                ioService.WriteLine($"File #{i + 1} of {mdFilesPaths.Length}: {mdFileRelativePath}");

                ArchiveMdFileDto videoDataInfoDto;
                try
                {
                    var content = await File.ReadAllTextAsync(mdFilePath);
                    videoDataInfoDto = DeserializeYamlContent(content);

                    ioService.WriteLine("\tParsed md file");
                }
                catch (Exception ex) when (ex is InvalidDataException or YamlException)
                {
                    ioService.WriteErrorLine($"Error parsing metadata from md file \"{mdFilePath}\"");
                    ioService.PrintException(ex);

                    continue;
                }
                
                videosMetadata.Add((videoDataInfoDto, mdFileRelativePath));
            }

            return videosMetadata.Select(
                p => new MdFileVideoMetadata(
                    p.mdDto.Title,
                    p.mdDto.Description,
                    p.mdRelativePath,
                    youtubeDownloader,
                    p.mdDto.YoutubeUrl,
                    p.mdDto.EthernaIndex,
                    p.mdDto.EthernaPermalink));
        }

        // Helpers.
        private ArchiveMdFileDto DeserializeYamlContent(string content)
        {
            //extract body
            var yamlMatch = YamlBodyRegex().Match(content);
            if (!yamlMatch.Success)
                throw new InvalidDataException("Document not well formatted");

            string yamlBodyString = yamlMatch.Groups["body"].Value;

            //normalize multiline fields
            string fixedYaml;
            {
                string[] lines = yamlBodyString.Split('\n');
                var fixedYamlBuilder = new StringBuilder();

                bool isInString = false;
                foreach (var line in lines)
                {
                    //add an empty line if necessary. Deserializer only read "folded" scalar type and not "literal"
                    if (isInString)
                        fixedYamlBuilder.AppendLine();

                    //add two initial spaces is necessary
                    var fixedLine = isInString && (string.IsNullOrWhiteSpace(line) || !char.IsWhiteSpace(line[0])) ?
                        "  " + line.TrimEnd() :
                        line.TrimEnd();
                    fixedYamlBuilder.AppendLine(fixedLine);

                    //count unescaped quotes in the current line to identify open strings after endline
                    var quoteMatches = UnescapedQuotesCounterRegex().Matches(fixedLine);
                    if (quoteMatches.Count % 2 != 0)
                        isInString = !isInString;
                }

                fixedYaml = fixedYamlBuilder.ToString();
            }

            //deserialize
            return deserializer.Deserialize<ArchiveMdFileDto>(fixedYaml) ??
                throw new InvalidDataException("Can't parse valid YAML metadata");
        }
    }
}
