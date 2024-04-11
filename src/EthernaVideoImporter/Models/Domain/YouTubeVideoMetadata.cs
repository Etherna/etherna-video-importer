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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode.Exceptions;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class YouTubeVideoMetadata : YouTubeVideoMetadataBase
    {
        // Consts.
        public TimeSpan FetchRetryDelay = TimeSpan.FromMinutes(10);
        public int FetchRetryMax = 20;
        
        // Constructors.
        internal YouTubeVideoMetadata(
            IYoutubeDownloader youtubeDownloader,
            string youtubeUrl)
            : base(youtubeDownloader, youtubeUrl)
        { }

        // Properties.
        public override string Id => YoutubeId;
        
        // Methods.
        public override async Task<bool> TryFetchMetadataAsync()
        {
            /*
             * YouTube could block fetches to avoid data scrapping.
             * If this happens, we need to retry with enough delay.
             */
            for (int i = 0; i < FetchRetryMax; i++)
            {
                try
                {
                    var metadata = await YoutubeDownloader.YoutubeClient.Videos.GetAsync(YoutubeUrl);
                    var bestStreamInfo =
                        (await YoutubeDownloader.YoutubeClient.Videos.Streams.GetManifestAsync(metadata.Id))
                        .GetVideoOnlyStreams()
                        .OrderByDescending(s => s.VideoResolution.Area)
                        .First();

                    Description = metadata.Description;
                    Duration = metadata.Duration ??
                               throw new InvalidOperationException("Live streams are not supported");
                    OriginVideoQualityLabel = bestStreamInfo.VideoQuality.Label;
                    Thumbnail = metadata.Thumbnails.MaxBy(t => t.Resolution.Area);
                    Title = metadata.Title;

                    Console.WriteLine($"Downloaded metadata for {metadata.Title}");

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

                if (i + 1 < FetchRetryMax)
                {
                    Console.WriteLine($"Retry in {FetchRetryDelay.TotalMinutes} minutes");
                    await Task.Delay(FetchRetryDelay);
                }
            }

            return false;
        }
    }
}
