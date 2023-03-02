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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Devcon.Services
{
    internal sealed class MdVideoProvider : IVideoProvider
    {
        // Consts.
        public static readonly string[] _keywordNames = { "IMAGE", "IMAGEURL", "EDITION", "TITLE", "DESCRIPTION", "YOUTUBEURL", "IPFSHASH", "DURATION", "EXPERTISE", "TYPE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS", "ETHERNAINDEX", "ETHERNAPERMALINK", "SOURCEID" };

        // Fields.
        public static readonly string[] _keywordForArrayString = Array.Empty<string>();
        private readonly bool includeAudioTrack;
        private readonly string mdFolderRootPath;
        private readonly YoutubeClient youtubeClient;
        private readonly IYoutubeDownloader youtubeDownloader;

        // Constructor.
        public MdVideoProvider(
            string mdFolderRootPath,
            string ffMpegPath,
            bool includeAudioTrack)
        {
            this.mdFolderRootPath = mdFolderRootPath;
            this.includeAudioTrack = includeAudioTrack;
            youtubeClient = new();
            youtubeDownloader = new YoutubeDownloader(ffMpegPath, youtubeClient);
        }

        // Properties.
        public string SourceName => mdFolderRootPath;

        // Methods.
        public Task<Video> GetVideoAsync(VideoMetadataBase videoMetadata) => youtubeDownloader.GetVideoAsync(
            includeAudioTrack,
            videoMetadata as MdFileVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(MdFileVideoMetadata)}", nameof(videoMetadata)));

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var mdFilesPaths = Directory.GetFiles(mdFolderRootPath, "*.md", SearchOption.AllDirectories);

            Console.WriteLine($"Found {mdFilesPaths.Length} videos");

            var videosMetadata = new List<(ArchiveMdFileDto mdDto, YoutubeExplode.Videos.Video ytVideo, VideoOnlyStreamInfo ytBestStreamInfo, string mdRelativePath)>();
            foreach (var mdFilePath in mdFilesPaths)
            {
                // Get from md file.
                var mdConvertedToJson = new StringBuilder();
                var markerLine = 0;
                var keyFound = 0;
                var descriptionExtraRows = new List<string>();
                ArchiveMdFileDto? videoDataInfoDto = null;
                foreach (var line in File.ReadLines(mdFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line == "---")
                    {
                        markerLine++;

                        if (markerLine == 1)
                            mdConvertedToJson.AppendLine("{");
                        else if (markerLine == 2)
                        {
                            mdConvertedToJson.AppendLine("}");
                            try
                            {
                                videoDataInfoDto = JsonSerializer.Deserialize<ArchiveMdFileDto>(
                                    mdConvertedToJson.ToString(),
                                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{ex.Message} \n Unable to parse file: {mdFilePath}");
                            }

                            markerLine = 0;
                            keyFound = 0;
                            mdConvertedToJson = new StringBuilder();
                            videoDataInfoDto?.AddDescription(descriptionExtraRows);
                        }
                    }
                    else
                    {
                        mdConvertedToJson.AppendLine(FormatLineForJson(line, keyFound == 0, descriptionExtraRows));
                        keyFound++;
                    }
                }

                if (videoDataInfoDto is null)
                    continue;

                // Get from youtube.
                var youtubeVideo = await youtubeClient.Videos.GetAsync(videoDataInfoDto.YoutubeUrl);
                var youtubeBestStreamInfo = (await youtubeClient.Videos.Streams.GetManifestAsync(youtubeVideo.Id))
                    .GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoResolution.Area)
                    .First();

                videosMetadata.Add((videoDataInfoDto, youtubeVideo, youtubeBestStreamInfo, Path.GetRelativePath(mdFilePath, mdFolderRootPath)));
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
        private static string FormatLineForJson(string line, bool isFirstRow, List<string> descriptionExtraRows)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            // Fix error declaration of speakers.
            line = line.Replace("\"G. Nicholas D'Andrea\"", "'G. Nicholas D\"Andrea'", StringComparison.InvariantCultureIgnoreCase);
            if (line.Contains("", StringComparison.InvariantCultureIgnoreCase))
                if (_keywordForArrayString.Any(keyArrayString =>
                        line.StartsWith(keyArrayString, StringComparison.InvariantCultureIgnoreCase) &&
                        line.Contains("['", StringComparison.InvariantCultureIgnoreCase)))
                {
                    //array of string change from ' to "
                    //use TEMPSINGLEQUOTE for manage the case like 'Piotrek "Viggith" Janiuk'
                    line = line.Replace("'", "TEMPSINGLEQUOTE", StringComparison.InvariantCultureIgnoreCase);
                    line = line.Replace("\"", "'", StringComparison.InvariantCultureIgnoreCase);
                    line = line.Replace("TEMPSINGLEQUOTE", "\"", StringComparison.InvariantCultureIgnoreCase);
                }

            // Prevent multiline description error 
            if (!_keywordNames.Any(keywordName => line.StartsWith(keywordName, StringComparison.InvariantCultureIgnoreCase)))
            {
                descriptionExtraRows.Add(line);
                return "";
            }

            var formatedString = (isFirstRow ? "" : ",") // Add , at end of every previus row (isFirstKeyFound used to avoid insert , in the last keyword)
                + "\"" // Add " at start of every row
                + ReplaceFirstOccurrence(line, ":", "\":"); // Find the first : and add "

            // Prevent error for description multiline
            if (line.StartsWith("DESCRIPTION", StringComparison.InvariantCultureIgnoreCase) &&
                !formatedString.EndsWith("\"", StringComparison.InvariantCultureIgnoreCase))
                formatedString += "\"";

            return formatedString.Replace("\t", " ", StringComparison.InvariantCultureIgnoreCase); // Replace \t \ with space
        }

        private static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "";

            var index = source.IndexOf(find, StringComparison.InvariantCultureIgnoreCase);
            string result = source.Remove(index, find.Length).Insert(index, replace);
            return result;
        }
    }
}
