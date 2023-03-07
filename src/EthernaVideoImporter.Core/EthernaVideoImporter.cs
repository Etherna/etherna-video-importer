﻿//   Copyright 2022-present Etherna Sagl
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

using Etherna.ServicesClient.Clients.Gateway;
using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
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
        private readonly IUserGatewayClient ethernaGatewayClient;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly ILinkReporterService linkReporterService;
        private readonly IVideoService videoService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            IUserGatewayClient ethernaGatewayClient,
            IUserIndexClient ethernaIndexClient,
            ILinkReporterService linkReporterService,
            IVideoProvider videoProvider,
            IVideoService videoService)
        {
            if (linkReporterService is null)
                throw new ArgumentNullException(nameof(linkReporterService));
            if (videoProvider is null)
                throw new ArgumentNullException(nameof(videoProvider));
            if (videoService is null)
                throw new ArgumentNullException(nameof(videoService));

            this.ethernaGatewayClient = ethernaGatewayClient;
            this.ethernaIndexClient = ethernaIndexClient;
            this.linkReporterService = linkReporterService;
            this.videoProvider = videoProvider;
            this.videoService = videoService;
        }

        // Public methods.
        public async Task RunAsync(
            string userEthAddress,
            bool offerVideos,
            bool pinVideos,
            bool deleteVideosRemovedFromSource,
            bool deleteExogenousVideos,
            bool unpinRemovedVideos)
        {
            // Get video info.
            Console.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync();
            var totalSourceVideo = sourceVideosMetadata.Count();

            Console.WriteLine($"Found {sourceVideosMetadata.Count()} valid videos from source");

            // Get information from etherna index.
            Console.WriteLine("Get user's videos on etherna index");

            var userVideosOnIndex = await GetUserVideosOnEthernaAsync(userEthAddress);
            var ethernaIndexParameters = await ethernaIndexClient.SystemClient.ParametersAsync();

            Console.WriteLine($"Found {userVideosOnIndex.Count()} videos already published on etherna index");

            // Import each video.
            Console.WriteLine("Start importing videos");

            foreach (var (sourceMetadata, i) in sourceVideosMetadata.Select((m, i) => (m, i)))
            {
                string updatedIndexId;
                string updatedPermalinkHash;

                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Processing video #{i + 1} of #{totalSourceVideo}. Source Id: {sourceMetadata.Id}");
                    Console.WriteLine($"Title: {sourceMetadata.Title}");

                    // Search already uploaded video. Compare Id serialized on manifest personal data with metadata Id from source.
                    var alreadyPresentVideo = userVideosOnIndex.FirstOrDefault(
                        v => v.LastValidManifest?.PersonalData?.VideoId == sourceMetadata.Id);

                    if (alreadyPresentVideo != null)
                    {
                        Console.WriteLine("Video already uploaded on etherna");

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
                            updatedPermalinkHash = await videoService.UploadVideoManifestAsync(updatedManifest, pinVideos);

                            // Update on index.
                            await ethernaIndexClient.VideosClient.VideosPutAsync(
                                alreadyPresentVideo.IndexId,
                                updatedPermalinkHash);
                        }

                        // Pin all.
                        if (pinVideos)
                            await videoService.PinVideoAsync(alreadyPresentVideo.LastValidManifest, updatedPermalinkHash);
                    }
                    else //try to upload new video on etherna
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
                        var video = await videoProvider.GetVideoAsync(sourceMetadata);

                        if (!video.EncodedFiles.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: can't get valid stream from source\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload video and all related data.
                        await videoService.UploadVideoAsync(video, pinVideos, offerVideos);

                        updatedIndexId = video.EthernaIndexId!;
                        updatedPermalinkHash = video.EthernaPermalinkHash!;
                    }

                    // Import succeeded.
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"#{i + 1} Video imported successfully");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("Video unable to upload");
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();

                    continue;
                }

                // Report etherna new references.
                try
                {
                    /*
                     * Report etherna references even if video is already present on index.
                     * This handle cases where references are missing for some previous execution error.
                     */
                    await linkReporterService.SetEthernaReferencesAsync(
                        sourceMetadata.Id,
                        updatedIndexId,
                        updatedPermalinkHash);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("Unable to report etherna links");
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();

                    continue;
                }
            }

            // Clean up user channel on etherna index.
            IEnumerable<string>? gatewayPinnedHashes = null;
            if (unpinRemovedVideos)
                gatewayPinnedHashes = await ethernaGatewayClient.UsersClient.PinnedResourcesAsync();

            if (deleteVideosRemovedFromSource)
                await videoService.DeleteVideosRemovedFromSourceAsync(
                    sourceVideosMetadata,
                    userVideosOnIndex,
                    gatewayPinnedHashes,
                    unpinRemovedVideos);

            if (deleteExogenousVideos)
                await videoService.DeleteExogenousVideosAsync(
                    userVideosOnIndex,
                    gatewayPinnedHashes,
                    unpinRemovedVideos);
        }

        // Helpers.
        public async Task<IEnumerable<IndexedVideo>> GetUserVideosOnEthernaAsync(string userAddress)
        {
            var videos = new List<VideoDto>();
            const int MaxForPage = 100;

            VideoDtoPaginatedEnumerableDto? page = null;
            do
            {
                page = await ethernaIndexClient.UsersClient.Videos2Async(userAddress, page is null ? 0 : page.CurrentPage + 1, MaxForPage);
                videos.AddRange(page.Elements);
            } while (page.Elements.Any());

            return videos.Select(v => new IndexedVideo(v));
        }
    }
}
