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
using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class Runner
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly ILinkReporterService linkReporterService;
        private readonly IVideoDownloaderService videoDownloaderService;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public Runner(
            ICleanerVideoService cleanerVideoService,
            IUserIndexClient ethernaIndexClient,
            ILinkReporterService linkReporterService,
            IVideoDownloaderService videoDownloaderService,
            IVideoProvider videoProvider,
            IVideoUploaderService videoUploaderService)
        {
            if (cleanerVideoService is null)
                throw new ArgumentNullException(nameof(cleanerVideoService));
            if (linkReporterService is null)
                throw new ArgumentNullException(nameof(linkReporterService));
            if (videoDownloaderService is null)
                throw new ArgumentNullException(nameof(videoDownloaderService));
            if (videoProvider is null)
                throw new ArgumentNullException(nameof(videoProvider));
            if (videoUploaderService is null)
                throw new ArgumentNullException(nameof(videoUploaderService));

            this.cleanerVideoService = cleanerVideoService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.linkReporterService = linkReporterService;
            this.videoDownloaderService = videoDownloaderService;
            this.videoProvider = videoProvider;
            this.videoUploaderService = videoUploaderService;
        }

        // Public methods.
        public async Task RunAsync(
            bool offerVideo, 
            bool pinVideo, 
            bool deleteOldVideo,
            bool deleteVideosFromOtherSources,
            string userEthAddr)
        {
            // Get info from index.
            var importedVideos = await GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);
            var indexParams = await ethernaIndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);

            // Get video info.
            Console.WriteLine("Get videos metadata");
            var videosMetadata = await videoProvider.GetVideosMetadataAsync().ConfigureAwait(false);

            // Import each video.
            var totalVideo = videosMetadata.Count();
            foreach (var (video, i) in videosMetadata.Select((vi, i) => (vi, i)))
            {
                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{i} of #{totalVideo}");
                    Console.WriteLine($"Title: {video.Title}");

                    // Find index video Id.
                    var indexVideoId = importedVideos.Select(videoData =>
                        new
                        {
                            IndexVideoId = videoData.Id,
                            PersonalData = JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)
                        })
                        .Where(v => v?.PersonalData?.VideoId == video.YoutubeId)
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
                            personalData.VideoId == video.YoutubeId)
                        {
                            // When YoutubeId is already uploaded, check for any change in metadata.
                            if (video.Title == lastValidManifest.Title &&
                                video.Description == lastValidManifest.Description)
                            {
                                // No change in any fields.
                                Console.WriteLine($"Video already on etherna");
                                continue;
                            }
                            else
                            {
                                // Edit manifest data fields.
                                lastValidManifest.Description = video.Description ?? "";
                                lastValidManifest.Title = video.Title ?? "";
                            }
                        }
                        else
                        {
                            // Youtube video changed.
                            video.ResetEthernaData(); // Reset all data otherwise instead of create new index will be update.
                            lastValidManifest = null; // Set null for restart all process like a first time.
                        }
                    }

                    // Data validation.
                    if (video.Title!.Length > indexParams.VideoTitleMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Title too long, max: {indexParams.VideoTitleMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    if (video.Description!.Length > indexParams.VideoDescriptionMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Description too long, max: {indexParams.VideoDescriptionMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine($"Source Video: {video.YoutubeUrl}");

                    if (lastValidManifest is null)
                    {
                        // Download from source.
                        var videoData = await videoDownloaderService.StartDownloadAsync(video).ConfigureAwait(false);

                        if (videoData?.VideoDataResolutions is null ||
                            videoData.VideoDataResolutions.Count <= 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: video for download not found\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload on bee node.
                        await videoUploaderService.UploadVideoAsync(videoData, pinVideo, offerVideo).ConfigureAwait(false);
                    }
                    else
                    {
                        // Change metadata info.
                        var hashMetadataReference = await videoUploaderService.UploadMetadataAsync(lastValidManifest, video, pinVideo).ConfigureAwait(false);
                        await UpsertManifestToIndex(hashMetadataReference, video).ConfigureAwait(false);
                    }

                    // Save MD file with etherna values.
                    Console.WriteLine($"Save etherna values in file {video.MdFilepath}\n");
                    await linkReporterService.SetEthernaFieldsAsync(
                        video.MdFilepath!,
                        video.EthernaIndex!,
                        video.EthernaPermalink!).ConfigureAwait(false);

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
            if (deleteOldVideo)
                await cleanerVideoService.RunOldDeleterAsync(importedVideos).ConfigureAwait(false);

            if (deleteVideosFromOtherSources)
                await cleanerVideoService.RunCleanerAsync(videosMetadata, importedVideos).ConfigureAwait(false);
        }

        // Helpers.
        public async Task<IEnumerable<VideoDto>> GetAllUserVideoAsync(string userAddress)
        {
            var elements = new List<VideoDto>();
            const int MaxForPage = 100;

            for (var currentPage = 0; true; currentPage++)
            {
                var result = await ethernaIndexClient.UsersClient.Videos2Async(userAddress, currentPage, MaxForPage).ConfigureAwait(false);

                if (result?.Elements is null ||
                    !result.Elements.Any())
                    return elements;

                elements.AddRange(result.Elements);
            }
        }

        private async Task<string> UpsertManifestToIndex(
            string hashReferenceMetadata,
            VideoMetadata videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            if (!string.IsNullOrEmpty(videoData.IndexVideoId))
            {
                // Update manifest index.
                Console.WriteLine($"Update Index: {videoData!.IndexVideoId}\t{hashReferenceMetadata}");

                await ethernaIndexClient.VideosClient.VideosPutAsync(videoData.IndexVideoId!, hashReferenceMetadata).ConfigureAwait(false);

                return videoData.IndexVideoId!;
            }
            else
            {
                // Create new manifest index.
                Console.WriteLine($"Create Index: {hashReferenceMetadata}");

                var videoCreateInput = new VideoCreateInput
                {
                    ManifestHash = hashReferenceMetadata,
                };
                var indexVideoId = await ethernaIndexClient.VideosClient.VideosPostAsync(videoCreateInput).ConfigureAwait(false);

                videoData.SetEthernaIndex(indexVideoId);

                return indexVideoId;
            }
        }
    }
}
