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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class EthernaVideoImporter
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            ICleanerVideoService cleanerVideoService,
            IUserIndexClient ethernaIndexClient,
            IVideoProvider videoProvider,
            IVideoUploaderService videoUploaderService)
        {
            if (cleanerVideoService is null)
                throw new ArgumentNullException(nameof(cleanerVideoService));
            if (videoProvider is null)
                throw new ArgumentNullException(nameof(videoProvider));
            if (videoUploaderService is null)
                throw new ArgumentNullException(nameof(videoUploaderService));

            this.cleanerVideoService = cleanerVideoService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.videoProvider = videoProvider;
            this.videoUploaderService = videoUploaderService;
        }

        // Public methods.
        public async Task RunAsync(
            string userEthAddress,
            bool offerVideos,
            bool pinVideos,
            bool deleteVideosRemovedFromSource,
            bool deleteVideosNotFromThisTool,
            Func<string, string, string, Task>? reporterLink)
        {
            // Get video info.
            Console.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync().ConfigureAwait(false);
            var totalSourceVideo = sourceVideosMetadata.Count();

            Console.WriteLine($"Found {sourceVideosMetadata.Count()} videos from source");

            // Get information from Etherna index.
            Console.WriteLine("Get user's videos on Etherna Index");

            var userVideosOnIndex = await GetUserVideosOnEthernaAsync(userEthAddress).ConfigureAwait(false);
            var ethernaIndexParameters = await ethernaIndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);

            Console.WriteLine($"Found {userVideosOnIndex.Count()} videos already published on Etherna Index");

            // Import each video.
            Console.WriteLine("Start importing videos");

            foreach (var (sourceMetadata, i) in sourceVideosMetadata.Select((m, i) => (m, i)))
            {
                string updatedIndexId;
                string updatedPermalinkHash;

                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Processing video #{i} of #{totalSourceVideo}. Source Id: {sourceMetadata.Id}");
                    Console.WriteLine($"Title: {sourceMetadata.Title}");

                    // Search already uploaded video. Compare Id serialized on manifest personal data with metadata Id from source.
                    var alreadyPresentVideo = userVideosOnIndex.FirstOrDefault(
                        v => v.LastValidManifest?.PersonalData?.VideoId == sourceMetadata.Id);

                    if (alreadyPresentVideo != null)
                    {
                        Console.WriteLine("Video already uploaded on Etherna");

                        // Verify if manifest needs to be updated with new metadata.
                        updatedIndexId = alreadyPresentVideo.IndexId;

                        if (alreadyPresentVideo.IsEqualTo(sourceMetadata))
                        {
                            updatedPermalinkHash = alreadyPresentVideo.LastValidManifest!.Hash;
                        }
                        else
                        {
                            Console.WriteLine($"Metadata has changed, update the video manifest");

                            var updatedManifest = new ManifestDto(
                                sourceMetadata.Title,
                                sourceMetadata.Description,
                                alreadyPresentVideo.LastValidManifest!.OriginalQuality,
                                userEthAddress,
                                alreadyPresentVideo.LastValidManifest.Duration,
                                new ManifestThumbnailDto(
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.AspectRatio,
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.Blurhash,
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.Sources),
                                alreadyPresentVideo.LastValidManifest.Sources.Select(s => new ManifestVideoSourceDto(s)),
                                alreadyPresentVideo.CreationDateTime.ToUnixTimeMilliseconds(), //this should be retrieved from manifest. See: https://etherna.atlassian.net/browse/EVI-60
                                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                alreadyPresentVideo.LastValidManifest.BatchId,
                                JsonSerializer.Serialize(ManifestPersonalDataDto.BuildNew(sourceMetadata.Id!)));

                            // Upload new manifest.
                            updatedPermalinkHash = await videoUploaderService.UploadVideoManifestAsync(
                                updatedManifest,
                                pinVideos).ConfigureAwait(false);

                            // Update on index.
                            await ethernaIndexClient.VideosClient.VideosPutAsync(
                                alreadyPresentVideo.IndexId,
                                updatedPermalinkHash).ConfigureAwait(false);
                        }
                    }
                    else //try to upload new video on Etherna
                    {
                        // Validate metadata.
                        if (sourceMetadata.Title.Length > ethernaIndexParameters.VideoTitleMaxLength)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: Title too long, max length: {ethernaIndexParameters.VideoTitleMaxLength}\n");
                            Console.ResetColor();
                            continue;
                        }
                        if (sourceMetadata.Description.Length > ethernaIndexParameters.VideoDescriptionMaxLength)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: Description too long, max length: {ethernaIndexParameters.VideoDescriptionMaxLength}\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Get and encode video from source.
                        var video = await videoProvider.GetVideoAsync(sourceMetadata).ConfigureAwait(false);

                        if (!video.EncodedFiles.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: can't get valid stream from source\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload video and all related data.
                        await videoUploaderService.UploadVideoAsync(video, pinVideos, offerVideos).ConfigureAwait(false);

                        updatedIndexId = video.EthernaIndexId!;
                        updatedPermalinkHash = video.EthernaPermalinkHash!;
                    }

                    // Report Etherna new references.
                    /*
                     * Report Etherna references even if video is already present on index.
                     * This handle cases where references are missing for some previous execution error.
                     */
                    if (reporterLink is not null)
                        await reporterLink(
                            sourceMetadata.Id,
                            updatedIndexId,
                            updatedPermalinkHash).ConfigureAwait(false);

                    // Import succeeded.
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
                await cleanerVideoService.RunCleanerAsync(sourceVideosMetadata, userVideosOnIndex).ConfigureAwait(false);
        }

        // Helpers.
        public async Task<IEnumerable<IndexedVideo>> GetUserVideosOnEthernaAsync(string userAddress)
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

            return videos.Select(v => new IndexedVideo(v));
        }
    }
}
