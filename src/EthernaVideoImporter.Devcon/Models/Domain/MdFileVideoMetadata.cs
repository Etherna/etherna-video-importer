//   Copyright 2022-present Etherna SA
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
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode.Exceptions;

namespace Etherna.VideoImporter.Devcon.Models.Domain
{
    public class MdFileVideoMetadata : YouTubeVideoMetadataBase
    {
        // Constructor.
        public MdFileVideoMetadata(
            string title,
            string description,
            string mdFileRelativePath,
            IYoutubeDownloader youtubeDownloader,
            string youtubeUrl,
            string? ethernaIndexUrl,
            string? ethernaPermalinkUrl)
            : base(youtubeDownloader, youtubeUrl)
        {
            if (string.IsNullOrWhiteSpace(mdFileRelativePath))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(mdFileRelativePath));
            
            // Generate Id and old Ids.
            Id = mdFileRelativePath.Replace('\\', '/'); //use unix-like path
            OldIds = new[] { mdFileRelativePath.Replace('/', '\\') }; //migrate from windows-like path

            Description = description;
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFileRelativePath = mdFileRelativePath;
            Title = title;
        }

        // Properties.
        public override string Id { get; }
        public string? EthernaIndexUrl { get; }
        public string? EthernaPermalinkUrl { get; }
        public string MdFileRelativePath { get; }
        public override IEnumerable<string> OldIds { get; }
        
        // Methods.
        public override async Task<bool> TryFetchMetadataAsync()
        {
            try
            {
                var youtubeVideo = await YoutubeDownloader.YoutubeClient.Videos.GetAsync(YoutubeUrl);
                var youtubeBestStreamInfo =
                    (await YoutubeDownloader.YoutubeClient.Videos.Streams.GetManifestAsync(youtubeVideo.Id))
                    .GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoResolution.Area)
                    .First();

                Duration = youtubeVideo.Duration ?? throw new InvalidOperationException("Live streams are not supported");
                OriginVideoQualityLabel = youtubeBestStreamInfo.VideoQuality.Label;
                Thumbnail = youtubeVideo.Thumbnails.MaxBy(t => t.Resolution.Area);

                Console.WriteLine("Fetched YouTube metadata.");

                return true;
            }
            catch (HttpRequestException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error retrieving video: {YoutubeUrl}. Try again later");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
            catch (TimeoutException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Time out retrieving video: {YoutubeUrl}. Try again later");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
            catch (VideoUnplayableException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Unplayable video: {YoutubeUrl}");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
            catch (YoutubeExplodeException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Can't read information from YouTube: {YoutubeUrl}");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            return false;
        }
    }
}
