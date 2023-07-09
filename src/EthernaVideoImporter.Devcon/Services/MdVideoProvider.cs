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
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed partial class MdVideoProvider : IVideoProvider
    {
        // Consts.
        private const string EthernaIndexPrefix = "ethernaIndex:";
        private const string EthernaPermalinkPrefix = "ethernaPermalink:";

        [GeneratedRegex("(?<!\\\\)\"")]
        private static partial Regex UnescapedQuotesCounterRegex();

        [GeneratedRegex("^\\s*---(?<body>[\\s\\S]+)---\\s*$")]
        private static partial Regex YamlBodyRegex();

        // Fields.
        private readonly IDeserializer deserializer;
        private readonly MdVideoProviderOptions options;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public MdVideoProvider(
            IOptions<MdVideoProviderOptions> options,
            IYoutubeDownloader youtubeDownloader)
        {
            deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            this.options = options.Value;
            this.youtubeDownloader = youtubeDownloader;
        }

        // Properties.
        public string SourceName => options.MdSourceFolderPath;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            videoMetadata as MdFileVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(MdFileVideoMetadata)}", nameof(videoMetadata)),
            options.GenerateThumbnailWhenMissing);

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var mdFilesPaths = Directory.GetFiles(options.MdSourceFolderPath, "*.md", SearchOption.AllDirectories);

            Console.WriteLine($"Found {mdFilesPaths.Length} videos");

            var videosMetadata = new List<(ArchiveMdFileDto mdDto, YoutubeExplode.Videos.Video ytVideo, VideoOnlyStreamInfo ytBestStreamInfo, string mdRelativePath)>();
            foreach (var (mdFilePath, i) in mdFilesPaths.Select((f, i) => (f, i)))
            {
                var mdFileRelativePath = Path.GetRelativePath(options.MdSourceFolderPath, mdFilePath);

                Console.WriteLine($"File #{i + 1} of {mdFilesPaths.Length}: {mdFileRelativePath}");

                ArchiveMdFileDto videoDataInfoDto;
                try
                {
                    string content = File.ReadAllText(mdFilePath);
                    videoDataInfoDto = DeserializeYamlContent(content);

                    Console.Write($"\tparsed md file...");
                }
                catch (Exception ex) when (ex is InvalidDataException or YamlException)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error parsing metadata from md file \"{mdFilePath}\"");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();

                    continue;
                }

                // Get from youtube.
                try
                {
                    var youtubeVideo = await youtubeDownloader.YoutubeClient.Videos.GetAsync(videoDataInfoDto.YoutubeUrl);
                    var youtubeBestStreamInfo = (await youtubeDownloader.YoutubeClient.Videos.Streams.GetManifestAsync(youtubeVideo.Id))
                        .GetVideoOnlyStreams()
                        .OrderByDescending(s => s.VideoResolution.Area)
                        .First();

                    Console.WriteLine($" and downloaded YouTube metadata.");

                    videosMetadata.Add((videoDataInfoDto, youtubeVideo, youtubeBestStreamInfo, mdFileRelativePath));
                }
                catch (HttpRequestException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error retrieving video from YouTube. Try again later");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (VideoUnplayableException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Unplayable video from YouTube");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadata.Select(
                p => new MdFileVideoMetadata(
                    p.mdDto.Title,
                    p.mdDto.Description,
                    p.ytVideo.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                    p.ytBestStreamInfo.VideoQuality.Label,
                    p.ytVideo.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                    p.mdRelativePath,
                    p.mdDto.YoutubeUrl,
                    p.mdDto.EthernaIndex,
                    p.mdDto.EthernaPermalink));
        }

        public async Task ReportEthernaReferencesAsync(
            string sourceVideoId,
            string ethernaIndexId,
            string ethernaPermalinkHash)
        {
            var filePath = Path.Combine(options.MdSourceFolderPath, sourceVideoId);
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
                    //add an empty line if necessary. Deserializator only read "folded" scalar type and not "literal"
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
