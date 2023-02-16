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

using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.ManifestDtos;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class EthernaVideoImporter
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly ILinkReporterService linkReporterService;
        private readonly Action<string> printLogLineAction;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            ICleanerVideoService cleanerVideoService,
            IUserIndexClient ethernaIndexClient,
            ILinkReporterService linkReporterService,
            IVideoProvider videoProvider,
            IVideoUploaderService videoUploaderService,
            Action<string>? printLogLineAction)
        {
            if (cleanerVideoService is null)
                throw new ArgumentNullException(nameof(cleanerVideoService));
            if (linkReporterService is null)
                throw new ArgumentNullException(nameof(linkReporterService));
            if (videoProvider is null)
                throw new ArgumentNullException(nameof(videoProvider));
            if (videoUploaderService is null)
                throw new ArgumentNullException(nameof(videoUploaderService));

            this.cleanerVideoService = cleanerVideoService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.linkReporterService = linkReporterService;
            this.printLogLineAction = printLogLineAction ?? new Action<string>(_ => { });
            this.videoProvider = videoProvider;
            this.videoUploaderService = videoUploaderService;
        }

        // Public methods.
        public async Task RunAsync(
            string userEthAddress,
            bool offerVideos,
            bool pinVideos,
            bool deleteVideosRemovedFromSource,
            bool deleteVideosNotFromThisTool)
        {
            // Get information from Etherna index.
            var userVideosOnIndex = await GetUserVideosOnIndexAsync(userEthAddress).ConfigureAwait(false);
            var ethernaIndexParameters = await ethernaIndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);

            // Get video info.
            printLogLineAction("Get videos metadata");
            var videosMetadata = await videoProvider.GetVideosMetadataAsync().ConfigureAwait(false);

            // Import each video.
            var totalVideo = videosMetadata.Count();
            foreach (var (metadata, i) in videosMetadata.Select((vi, i) => (vi, i)))
            {
                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{i} of #{totalVideo}");
                    Console.WriteLine($"Title: {metadata.Title}");

                    // Find index video Id.
                    var indexVideoId = userVideosOnIndex.Select(videoData =>
                        new
                        {
                            IndexVideoId = videoData.Id,
                            PersonalData = JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)
                        })
                        .Where(v => v?.PersonalData?.VideoId == metadata.Id)
                        .FirstOrDefault()
                        ?.IndexVideoId;

                    // Check last valid manifest, if exist.
                    VideoManifestDto? lastValidManifest = null;
                    if (!string.IsNullOrWhiteSpace(indexVideoId))
                    {
                        try
                        {
                            var videoDto = await ethernaIndexClient.VideosClient.VideosGetAsync(indexVideoId).ConfigureAwait(false);
                            lastValidManifest = videoDto.LastValidManifest;
                        }
                        catch (IndexApiException ex) when (ex.StatusCode == 404) { }
                    }

                    if (lastValidManifest is not null)
                    {
                        // Check if manifest contain the same url of current md file.
                        var personalData = JsonUtility.FromJson<MetadataPersonalDataDto>(lastValidManifest.PersonalData ?? "{}");
                        if (personalData is not null &&
                            personalData.VideoId == metadata.Id)
                        {
                            // When YoutubeId is already uploaded, check for any change in metadata.
                            if (metadata.Title == lastValidManifest.Title &&
                                metadata.Description == lastValidManifest.Description)
                            {
                                // No change in any fields.
                                Console.WriteLine($"Video already on etherna");
                                continue;
                            }
                            else
                            {
                                // Edit manifest data fields.
                                lastValidManifest.Description = metadata.Description ?? "";
                                lastValidManifest.Title = metadata.Title ?? "";
                            }
                        }
                        else
                        {
                            // Youtube video changed.
                            metadata.ResetEthernaLinks(); // Reset all data otherwise instead of create new index will be update.
                            lastValidManifest = null; // Set null for restart all process like a first time.
                        }
                    }

                    // Data validation.
                    if (metadata.Title!.Length > ethernaIndexParameters.VideoTitleMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Title too long, max: {ethernaIndexParameters.VideoTitleMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    if (metadata.Description!.Length > ethernaIndexParameters.VideoDescriptionMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Description too long, max: {ethernaIndexParameters.VideoDescriptionMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine($"Source Video: {metadata.Id}");

                    if (lastValidManifest is null)
                    {
                        // Download from source.
                        var video = await videoProvider.GetVideoAsync(metadata).ConfigureAwait(false);

                        if (video.EncodedVideoFiles.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: video for download not found\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload on bee node.
                        await videoUploaderService.UploadVideoAsync(video, pinVideos, offerVideos).ConfigureAwait(false);
                    }
                    else
                    {
                        // Change metadata info.
                        var hashMetadataReference = await videoUploaderService.UploadMetadataAsync(lastValidManifest, metadata, pinVideos).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(metadata.EthernaIndexId))
                        {
                            // Update manifest index.
                            Console.WriteLine($"Update Index: {metadata.EthernaIndexId}\t{hashMetadataReference}");

                            await ethernaIndexClient.VideosClient.VideosPutAsync(metadata.EthernaIndexId!, hashMetadataReference).ConfigureAwait(false);
                        }
                        else
                        {
                            // Create new manifest index.
                            Console.WriteLine($"Create Index: {hashMetadataReference}");

                            var videoCreateInput = new VideoCreateInput
                            {
                                ManifestHash = hashMetadataReference,
                            };
                            var indexVideoId = await ethernaIndexClient.VideosClient.VideosPostAsync(videoCreateInput).ConfigureAwait(false);

                            metadata.EthernaIndexId = indexVideoId;
                        }
                    }

                    // Save MD file with etherna values.
                    Console.WriteLine($"Save etherna values in file {metadata.MdFilePath}\n");
                    await linkReporterService.SetEthernaFieldsAsync(
                        metadata.MdFilePath!,
                        metadata.EthernaIndex!,
                        metadata.EthernaPermalink!).ConfigureAwait(false);

                    // Import completed.
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"#{i} Video imported successfully");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n#{i} Video unable to import\n");
                    Console.ResetColor();
                }

                Console.WriteLine($"Done.");
            }

            // Clean up user channel on Etherna index.
            if (deleteVideosRemovedFromSource)
                await cleanerVideoService.RunOldDeleterAsync(userVideosOnIndex).ConfigureAwait(false);

            if (deleteVideosNotFromThisTool)
                await cleanerVideoService.RunCleanerAsync(videosMetadata, userVideosOnIndex).ConfigureAwait(false);
        }

        // Helpers.
        public async Task<IEnumerable<VideoDto>> GetUserVideosOnIndexAsync(string userAddress)
        {
            var videos = new List<VideoDto>();
            const int MaxForPage = 100;

            VideoDtoPaginatedEnumerableDto? page = null;
            do
            {
                page = await ethernaIndexClient.UsersClient.Videos2Async(
                    userAddress,
                    page is null ? 0 : page.CurrentPage + 1,
                    MaxForPage).ConfigureAwait(false);
                videos.AddRange(page.Elements);
            } while (page.Elements.Any());

            return videos;
        }
    }
}
