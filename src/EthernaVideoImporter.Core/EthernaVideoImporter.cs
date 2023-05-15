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

using Etherna.ServicesClient;
using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using Etherna.VideoImporter.Core.Models.ModelView;
using Etherna.VideoImporter.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class EthernaVideoImporter : IEthernaVideoImporter
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IGatewayService gatewayClient;
        private readonly IEthernaUserClients ethernaUserClients;
        private readonly IMigrationService migrationService;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            ICleanerVideoService cleanerVideoService,
            IEthernaUserClients ethernaUserClients,
            IGatewayService gatewayClient,
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
            this.ethernaUserClients = ethernaUserClients;
            this.gatewayClient = gatewayClient;
            this.migrationService = migrationService;
            this.videoProvider = videoProvider;
            this.videoUploaderService = videoUploaderService;
        }

        // Public methods.
        public async Task RunAsync(
            bool deleteExogenousVideos,
            bool deleteVideosRemovedFromSource,
            bool forceVideoUpload,
            bool offerVideos,
            bool pinVideos,
            string userEthAddress,
            bool unpinRemovedVideos)
        {
            var importSummaryModelView = new ImportSummaryModelView();

            // Get video info.
            Console.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync();
            var totalSourceVideo = sourceVideosMetadata.Count();

            Console.WriteLine($"Found {sourceVideosMetadata.Count()} valid videos from source");

            // Get information from etherna index.
            Console.WriteLine("Get user's videos on etherna index");

            var userVideosOnIndex = await GetUserVideosOnEthernaAsync(userEthAddress);
            var ethernaIndexParameters = await ethernaUserClients.IndexClient.SystemClient.ParametersAsync();

            Console.WriteLine($"Found {userVideosOnIndex.Count()} videos already published on etherna index");

            // Import each video.
            Console.WriteLine("Start importing videos");

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
                    var alreadyPresentVideo = userVideosOnIndex.FirstOrDefault(
                        v => v.LastValidManifest?.PersonalData?.VideoIdHash == ManifestPersonalDataDto.HashVideoId(sourceMetadata.Id));

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

                            var videoSwarmFile = alreadyPresentVideo.LastValidManifest.Sources.Select(v => new VideoSwarmFile(v.Quality, v.Reference));

                            var video = new Video(videoMetadata, videoSwarmFile, thumbnailFiles);

                            // Calculate batch space and dilute if needed.
                            //get batch free space
                            var batchStat = await gatewayClient.GetBatchStatsAsync(alreadyPresentVideo.LastValidManifest.BatchId);
                            var batchUsage = batchStat.Utilization / Math.Pow(2, batchStat.Depth - batchStat.BucketDepth);
                            var batchTotalSpace = Math.Pow(2, batchStat.Depth) * 4096;
                            var batchUsed = batchTotalSpace * batchUsage;
                            var batchAvailable = batchTotalSpace - batchUsed;
                            //get bytes to upload
                            var bytesToUpload = video.GetTotalByteSizeToUpload() * 1.2; //keep 20% of tollerance
                            //check for dilute required.
                            if (bytesToUpload > batchAvailable) 
                            {
                                //calculate dilute deeps.
                                var batchDepth = batchStat.Depth + 1;
                                while ((Math.Pow(2, batchDepth) * 4096) - batchUsed < bytesToUpload)
                                    batchDepth++;

                                await gatewayClient.DilutePostageBatchAsync(alreadyPresentVideo.LastValidManifest.BatchId, batchDepth);
                            }

                            // Upload new manifest.
                            var metadataVideo = new ManifestDto(video, alreadyPresentVideo.LastValidManifest.BatchId, userEthAddress);
                            updatedPermalinkHash = await videoUploaderService.UploadVideoManifestAsync(metadataVideo, pinVideos, offerVideos);

                            // Update on index.
                            await ethernaUserClients.IndexClient.VideosClient.VideosPutAsync(
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
                        await videoUploaderService.UploadVideoAsync(video, pinVideos, offerVideos);

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
                gatewayPinnedHashes = await gatewayClient.GetPinnedResourcesAsync();

            if (deleteVideosRemovedFromSource)
                importSummaryModelView.TotDeletedRemovedFromSource = await cleanerVideoService.DeleteVideosRemovedFromSourceAsync(
                    sourceVideosMetadata,
                    userVideosOnIndex,
                    gatewayPinnedHashes,
                    unpinRemovedVideos);

            if (deleteExogenousVideos)
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
                page = await ethernaUserClients.IndexClient.UsersClient.Videos2Async(userAddress, page is null ? 0 : page.CurrentPage + 1, MaxForPage);
                videos.AddRange(page.Elements);
            } while (page.Elements.Any());

            return videos.Select(v => new IndexedVideo(v));
        }
    }
}
