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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Devcon.Models.Domain;
using Etherna.VideoImporter.Devcon.Models.MdDto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed partial class MdVideoProvider : IVideoProvider
    {
        // Consts.
        public static readonly string[] _keywordNames = { "IMAGE", "IMAGEURL", "EDITION", "TITLE", "DESCRIPTION", "YOUTUBEURL", "IPFSHASH", "DURATION", "EXPERTISE", "TYPE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS", "ETHERNAINDEX", "ETHERNAPERMALINK", "SOURCEID" };
        public static readonly string[] _keywordSkips = { "IMAGE", "IMAGEURL", "IPFSHASH", "EXPERTISE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS", "SOURCEID" };

        // Fields.
        public static readonly string[] _keywordForArrayString = Array.Empty<string>();
        private readonly bool includeAudioTrack;
        private readonly IEnumerable<int> supportedHeightResolutions;
        private readonly string mdFolderRootPath;
        private readonly YoutubeClient youtubeClient;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public MdVideoProvider(
            string mdFolderRootPath,
            IEncoderService encoderService,
            bool includeAudioTrack,
            IEnumerable<int> supportedHeightResolutions)
        {
            this.mdFolderRootPath = mdFolderRootPath;
            this.includeAudioTrack = includeAudioTrack;
            this.supportedHeightResolutions = supportedHeightResolutions;
            youtubeClient = new();
            youtubeDownloader = new YoutubeDownloader(encoderService, youtubeClient);
        }

        // Properties.
        public string SourceName => mdFolderRootPath;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata, DirectoryInfo importerTempDirectoryInfo) => youtubeDownloader.GetVideoAsync(
            videoMetadata as MdFileVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(MdFileVideoMetadata)}", nameof(videoMetadata)),
            importerTempDirectoryInfo,
            includeAudioTrack,
            supportedHeightResolutions);

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var mdFilesPaths = Directory.GetFiles(mdFolderRootPath, "*.md", SearchOption.AllDirectories);

            Console.WriteLine($"Found {mdFilesPaths.Length} videos");

            var videosMetadata = new List<(ArchiveMdFileDto mdDto, YoutubeExplode.Videos.Video ytVideo, VideoOnlyStreamInfo ytBestStreamInfo, string mdRelativePath)>();
            foreach (var (mdFilePath, i) in mdFilesPaths.Select((f, i) => (f, i)))
            {
                var mdFileRelativePath = Path.GetRelativePath(mdFolderRootPath, mdFilePath);

                Console.WriteLine($"File #{i + 1} of {mdFilesPaths.Length}: {mdFileRelativePath}");

                // Deserialize YAML section from MD.
                string content = File.ReadAllText(mdFilePath);
                var yamlMatch = YamlRegex().Match(content);
                string yamlString = yamlMatch.ToString().Trim('-');

                var deserializer = new DeserializerBuilder()
                                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                        .IgnoreUnmatchedProperties()
                                        .Build();
                var videoDataInfoDto = deserializer.Deserialize<ArchiveMdFileDto>(yamlString);

                if (videoDataInfoDto is null)
                    continue;

                Console.Write($"\tparsed md file...");

                // Get from youtube.
                try
                {
                    var youtubeVideo = await youtubeClient.Videos.GetAsync(videoDataInfoDto.YoutubeUrl);
                    var youtubeBestStreamInfo = (await youtubeClient.Videos.Streams.GetManifestAsync(youtubeVideo.Id))
                        .GetVideoOnlyStreams()
                        .OrderByDescending(s => s.VideoResolution.Area)
                        .First();

                    Console.WriteLine($" and downloaded YouTube metadata.");

                    videosMetadata.Add((videoDataInfoDto, youtubeVideo, youtubeBestStreamInfo, mdFileRelativePath));
                }
                catch (HttpRequestException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine();
                    Console.WriteLine($"Error retrieving video from YouTube. Try again later");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (VideoUnplayableException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine();
                    Console.WriteLine($"Unplayable video from YouTube");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadata.Select(
                p => new MdFileVideoMetadata(
                    p.mdDto.Description,
                    p.ytVideo.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                    p.ytBestStreamInfo.VideoQuality.Label,
                    p.ytVideo.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                    p.mdDto.Title,
                    p.mdRelativePath,
                    p.mdDto.YoutubeUrl,
                    p.mdDto.EthernaIndex,
                    p.mdDto.EthernaPermalink));
        }

        // Helpers.
        private static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "";

            var index = source.IndexOf(find, StringComparison.InvariantCultureIgnoreCase);
            string result = source.Remove(index, find.Length).Insert(index, replace);
            return result;
        }

        [GeneratedRegex("---\\r?\\n[\\s\\S]*?---")]
        private static partial Regex YamlRegex();
    }
}
