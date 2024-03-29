﻿//   Copyright 2022-present Etherna SA
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

using Etherna.Authentication;
using Etherna.Authentication.Native;
using Etherna.ServicesClient.GeneratedClients.Index;
using Etherna.ServicesClient.Users;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using Etherna.VideoImporter.Core.Models.ModelView;
using Etherna.VideoImporter.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class EthernaVideoImporter : IEthernaVideoImporter
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IEthernaOpenIdConnectClient ethernaOpenIdConnectClient;
        private readonly IEthernaSignInService ethernaSignInService;
        private readonly IGatewayService gatewayService;
        private readonly IMigrationService migrationService;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            ICleanerVideoService cleanerVideoService,
            IEthernaUserIndexClient ethernaIndexClient,
            IEthernaOpenIdConnectClient ethernaOpenIdConnectClient,
            IEthernaSignInService ethernaSignInService,
            IGatewayService gatewayService,
            IMigrationService migrationService,
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
            this.ethernaOpenIdConnectClient = ethernaOpenIdConnectClient;
            this.ethernaSignInService = ethernaSignInService;
            this.gatewayService = gatewayService;
            this.migrationService = migrationService;
            this.videoProvider = videoProvider;
            this.videoUploaderService = videoUploaderService;
        }

        // Public methods.
        public async Task RunAsync(
            bool removeUnrecognizedVideos,
            bool removeMissingVideosFromSource,
            bool forceVideoUpload,
            bool offerVideos,
            bool pinVideos,
            bool unpinRemovedVideos)
        {
            // Signin user.
            try
            {
                await ethernaSignInService.SignInAsync();
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine($"Error during authentication");
                Console.WriteLine(e.Message);
                throw;
            }
            catch (Win32Exception e)
            {
                Console.WriteLine($"Error opening browser on local system. Try to authenticate with API key.");
                Console.WriteLine(e.Message);
                throw;
            }

            // Get info from authenticated user.
            var userEthAddress = await ethernaOpenIdConnectClient.GetEtherAddressAsync();
            var userName = await ethernaOpenIdConnectClient.GetUsernameAsync();

            Console.WriteLine($"User {userName} authenticated");

            // Get video info.
            Console.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync();
            var totalSourceVideo = sourceVideosMetadata.Count();

            Console.WriteLine($"Found {totalSourceVideo} valid videos from source");

            // Get information from etherna index.
            Console.WriteLine("Get user's videos on etherna index");

            var userVideosOnIndex = await GetUserVideosOnEthernaAsync(userEthAddress);
            var ethernaIndexParameters = await ethernaIndexClient.SystemClient.ParametersAsync();

            Console.WriteLine($"Found {userVideosOnIndex.Count()} videos already published on etherna index");

            // Import each video.
            Console.WriteLine("Start importing videos");

            var importSummaryModelView = new ImportSummaryModelView();
            foreach (var (sourceMetadata, i) in sourceVideosMetadata.Select((m, i) => (m, i)))
            {
                string updatedIndexId;
                string updatedPermalinkHash;
                OperationType operationType;

                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Processing video #{i + 1} of #{totalSourceVideo}. Source Id: {sourceMetadata.Id}");
                    Console.WriteLine($"Title: {sourceMetadata.Title}");

                    // Search already uploaded video. Compare Id serialized on manifest personal data with metadata Id from source.
                    var allVideoIdHashes = sourceMetadata.OldIds.Append(sourceMetadata.Id)
                        .Select(id => ManifestPersonalDataDto.HashVideoId(id))
                        .ToList();
                    var alreadyPresentVideo = userVideosOnIndex.FirstOrDefault(
                        v => v.LastValidManifest?.PersonalData?.VideoIdHash is not null &&
                            allVideoIdHashes.Contains(v.LastValidManifest.PersonalData.VideoIdHash));

                    // Check if need a migration operation.
                    var minRequiredMigrationOp = migrationService.DecideOperation(alreadyPresentVideo?.LastValidManifest?.PersonalData);

                    //try to update only manifest, or to skip if possible
                    if (alreadyPresentVideo is not null &&
                        !forceVideoUpload &&
                        minRequiredMigrationOp is OperationType.Skip or OperationType.UpdateManifest)
                    {
                        Console.WriteLine("Video already uploaded on etherna");

                        // Verify if manifest needs to be updated with new metadata.
                        updatedIndexId = alreadyPresentVideo.IndexId;

                        //try to skip
                        if (alreadyPresentVideo.IsEqualTo(sourceMetadata) &&
                            minRequiredMigrationOp is OperationType.Skip)
                        {
                            operationType = OperationType.Skip;
                            updatedPermalinkHash = alreadyPresentVideo.LastValidManifest!.Hash;
                        }

                        //else update manifest
                        else
                        {
                            Console.WriteLine($"Metadata has changed, update the video manifest");

                            operationType = OperationType.UpdateManifest;

                            // Create manifest.
                            var thumbnailFiles = alreadyPresentVideo.LastValidManifest!.Thumbnail.Sources.Select(t =>
                                new ThumbnailSwarmFile(
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.AspectRatio,
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.Blurhash,
                                    t.Value,
                                    0,/*currently we don't have actual size. Acceptable workaround until it is provided in manifest*/
                                    int.Parse(t.Key.Replace("w", "", StringComparison.OrdinalIgnoreCase), CultureInfo.InvariantCulture))); 

                            var videoMetadata = new SwarmVideoMetadata(
                                sourceMetadata.Id,
                                sourceMetadata.Title,
                                sourceMetadata.Description,
                                TimeSpan.FromSeconds(alreadyPresentVideo.LastValidManifest!.Duration),
                                alreadyPresentVideo.LastValidManifest!.OriginalQuality);

                            var videoSwarmFile = alreadyPresentVideo.LastValidManifest.Sources.Select(v => new VideoSwarmFile(v.Size, v.Quality, v.Reference));

                            var video = new Video(videoMetadata, videoSwarmFile, thumbnailFiles);

                            // Upload new manifest.
                            var metadataVideo = await ManifestDto.BuildNewAsync(video, alreadyPresentVideo.LastValidManifest.BatchId, userEthAddress);
                            updatedPermalinkHash = await videoUploaderService.UploadVideoManifestAsync(metadataVideo, pinVideos, offerVideos);

                            // Update on index.
                            await ethernaIndexClient.VideosClient.VideosPutAsync(
                                alreadyPresentVideo.IndexId,
                                updatedPermalinkHash);
                        }
                    }

                    //else, full upload the new video on etherna
                    else
                    {
                        operationType = OperationType.ImportAll;

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
                        video.EthernaIndexId = alreadyPresentVideo?.IndexId;

                        if (!video.EncodedFiles.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: can't get valid stream from source\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload video and all related data.
                        await videoUploaderService.UploadVideoAsync(video, pinVideos, offerVideos, userEthAddress);

                        updatedIndexId = video.EthernaIndexId!;
                        updatedPermalinkHash = video.EthernaPermalinkHash!;
                    }

                    // Import succeeded.
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"#{i + 1} Video imported successfully");
                    Console.ResetColor();

                    // Summary count.
                    switch (operationType)
                    {
                        case OperationType.ImportAll:
                            importSummaryModelView.TotSuccessVideoImported++;
                            break;
                        case OperationType.UpdateManifest:
                            importSummaryModelView.TotUpdatedVideoImported++;
                            break;
                        case OperationType.Skip:
                            importSummaryModelView.TotSkippedVideoImported++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Timestamp: {DateTime.UtcNow}");
                    Console.WriteLine("Video unable to upload");
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();

                    importSummaryModelView.TotErrorVideoImported++;
                    continue;
                }
                finally {
                    try
                    {
                        // Clear tmp folder.
                        foreach (var file in CommonConsts.TempDirectory.GetFiles())
                            file.Delete();
                        foreach (var dir in CommonConsts.TempDirectory.GetDirectories())
                            dir.Delete(true);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Warning: unable to delete some files inside of {CommonConsts.TempDirectory.FullName}.");
                        Console.WriteLine($"Please remove manually after the process.");
                        Console.ResetColor();
                    }
                }

                // Report etherna new references.
                try
                {
                    /*
                     * Report etherna references even if video is already present on index.
                     * This handle cases where references are missing for some previous execution error.
                     */
                    await videoProvider.ReportEthernaReferencesAsync(
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
                gatewayPinnedHashes = await gatewayService.GetPinnedResourcesAsync();

            if (removeMissingVideosFromSource)
                importSummaryModelView.TotDeletedRemovedFromSource = await cleanerVideoService.DeleteVideosRemovedFromSourceAsync(
                    sourceVideosMetadata,
                    userVideosOnIndex,
                    gatewayPinnedHashes,
                    unpinRemovedVideos);

            if (removeUnrecognizedVideos)
                importSummaryModelView.TotDeletedExogenous = await cleanerVideoService.DeleteExogenousVideosAsync(
                    userVideosOnIndex,
                    gatewayPinnedHashes,
                    unpinRemovedVideos);

            // Print summary.
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Import completed.");
            Console.WriteLine();
            Console.WriteLine($"Total video processed: {importSummaryModelView.TotProcessedVideos}");
            Console.WriteLine($"Total video imported: {importSummaryModelView.TotSuccessVideoImported}");
            Console.WriteLine($"Total video updated: {importSummaryModelView.TotUpdatedVideoImported}");
            Console.WriteLine($"Total video skipped (already present): {importSummaryModelView.TotSkippedVideoImported}");
            Console.WriteLine($"Total video with errors: {importSummaryModelView.TotErrorVideoImported}");
            Console.WriteLine($"Total video deleted for missing in source: {importSummaryModelView.TotDeletedRemovedFromSource}");
            Console.WriteLine($"Total video deleted because not from this tool: {importSummaryModelView.TotDeletedExogenous}");
            Console.ResetColor();
        }

        // Helpers.
        private async Task<IEnumerable<IndexedVideo>> GetUserVideosOnEthernaAsync(string userAddress)
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
