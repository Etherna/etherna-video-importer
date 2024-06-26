﻿// Copyright 2022-present Etherna SA
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
    internal sealed partial class MdVideoProvider : IVideoProvider
    {
        // Consts.
        [GeneratedRegex("(?<!\\\\)\"")]
        private static partial Regex UnescapedQuotesCounterRegex();

        [GeneratedRegex("^\\s*---(?<body>[\\s\\S]+)---\\s*$")]
        private static partial Regex YamlBodyRegex();

        // Fields.
        private readonly IDeserializer deserializer;
        private readonly MdVideoProviderOptions options;
        private readonly IIoService ioService;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public MdVideoProvider(
            IIoService ioService,
            IOptions<MdVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            this.options = options.Value;
            this.ioService = ioService;
            this.youtubeDownloader = youtubeDownloader;
        }

        // Properties.
        public string SourceName => options.MdSourceFolderPath;

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
