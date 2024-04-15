// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.Authentication;
using Etherna.Authentication.Native;
using Etherna.Sdk.GeneratedClients.Index;
using Etherna.Sdk.Users;
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
        private readonly IAppVersionService appVersionService;
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IEthernaOpenIdConnectClient ethernaOpenIdConnectClient;
        private readonly IEthernaSignInService ethernaSignInService;
        private readonly IGatewayService gatewayService;
        private readonly IIoService ioService;
        private readonly IMigrationService migrationService;
        private readonly IResultReporterService resultReporterService;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoProvider videoProvider;

        // Constructor.
        public EthernaVideoImporter(
            IAppVersionService appVersionService,
            ICleanerVideoService cleanerVideoService,
            IEthernaUserIndexClient ethernaIndexClient,
            IEthernaOpenIdConnectClient ethernaOpenIdConnectClient,
            IEthernaSignInService ethernaSignInService,
            IGatewayService gatewayService,
            IIoService ioService,
            IMigrationService migrationService,
            IResultReporterService resultReporterService,
            IVideoProvider videoProvider,
            IVideoUploaderService videoUploaderService)
        {
            ArgumentNullException.ThrowIfNull(cleanerVideoService, nameof(cleanerVideoService));
            ArgumentNullException.ThrowIfNull(videoProvider, nameof(videoProvider));
            ArgumentNullException.ThrowIfNull(videoUploaderService, nameof(videoUploaderService));

            this.appVersionService = appVersionService;
            this.cleanerVideoService = cleanerVideoService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.ethernaOpenIdConnectClient = ethernaOpenIdConnectClient;
            this.ethernaSignInService = ethernaSignInService;
            this.gatewayService = gatewayService;
            this.ioService = ioService;
            this.migrationService = migrationService;
            this.resultReporterService = resultReporterService;
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
            bool unpinRemovedVideos,
            bool ignoreAppUpdates)
        {
            // Print startup header.
            ioService.WriteLine(
                $"""
                
                Etherna Video Importer (v{appVersionService.CurrentVersion})
                
                """, false);

            // Check for new app versions.
            try
            {
                var (lastVersion, lastVersionUrl) = await appVersionService.GetLastVersionAsync();
                if (lastVersion > appVersionService.CurrentVersion)
                {
                    ioService.WriteLine(
                        $"""
                         A new release is available: {lastVersion}
                         Upgrade now, or check out the release page at:
                          {lastVersionUrl}
                         """, false);

                    if (!ignoreAppUpdates)
                        return;
                }
            }
            catch (Exception e)
            {
                ioService.WriteErrorLine("Unable to verify last version on GitHub");
                ioService.PrintException(e);
            }
            
            // Signin user.
            try
            {
                await ethernaSignInService.SignInAsync();
            }
            catch (InvalidOperationException e)
            {
                ioService.WriteErrorLine("Error during authentication");
                ioService.PrintException(e);
                throw;
            }
            catch (Win32Exception e)
            {
                ioService.WriteErrorLine("Error opening browser on local system. Try to authenticate with API key.");
                ioService.PrintException(e);
                throw;
            }

            // Get info from authenticated user.
            var userEthAddress = await ethernaOpenIdConnectClient.GetEtherAddressAsync();
            var userName = await ethernaOpenIdConnectClient.GetUsernameAsync();

            ioService.WriteLine($"User {userName} authenticated");

            // Get video info.
            ioService.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync();
            var totalSourceVideo = sourceVideosMetadata.Count();

            ioService.WriteLine($"Found {totalSourceVideo} valid videos from source");

            // Get information from etherna index.
            ioService.WriteLine("Get user's videos on etherna index");

            var userVideosOnIndex = await GetUserVideosOnEthernaAsync(userEthAddress);
            var ethernaIndexParameters = await ethernaIndexClient.SystemClient.ParametersAsync();

            ioService.WriteLine($"Found {userVideosOnIndex.Count()} videos already published on etherna index");

            // Import each video.
            ioService.WriteLine("Start importing videos");

            var importSummaryModelView = new ImportSummaryModelView();
            var results = new List<VideoImportResultBase>();
            foreach (var (sourceMetadata, i) in sourceVideosMetadata.Select((m, i) => (m, i)))
            {
                VideoImportResultBase importResult;

                try
                {
                    ioService.WriteLine("===============================", false);
                    ioService.WriteLine($"Processing video #{i + 1} of #{totalSourceVideo}. Source Id: {sourceMetadata.Id}");

                    if (!sourceMetadata.IsDataFetched &&
                        !await sourceMetadata.TryFetchMetadataAsync(ioService))
                        throw new InvalidOperationException("Can't fetch source metadata");

                    ioService.WriteLine($"Title: {sourceMetadata.Title}", false);

                    // Search already uploaded video. Compare Id serialized on manifest personal data with metadata Id from source.
                    var allVideoIdHashes = sourceMetadata.OldIds.Append(sourceMetadata.Id)
                        .Select(ManifestPersonalDataDto.HashVideoId)
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
                        ioService.WriteLine("Video already uploaded on etherna");

                        // Verify if manifest needs to be updated with new metadata.
                        //try to skip
                        if (alreadyPresentVideo.IsEqualTo(sourceMetadata) &&
                            minRequiredMigrationOp is OperationType.Skip)
                        {
                            importResult = VideoImportResultSucceeded.Skipped(
                                sourceMetadata,
                                alreadyPresentVideo.IndexId,
                                alreadyPresentVideo.LastValidManifest!.Hash);
                        }

                        //else update manifest
                        else
                        {
                            ioService.WriteLine($"Metadata has changed, update the video manifest");

                            // Create manifest.
                            var thumbnailFiles = alreadyPresentVideo.LastValidManifest!.Thumbnail.Sources.Select(t =>
                                new ThumbnailSwarmFile(
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.AspectRatio,
                                    alreadyPresentVideo.LastValidManifest.Thumbnail.Blurhash,
                                    t.Value,
                                    0, /*currently we don't have actual size. Acceptable workaround until it is provided in manifest*/
                                    int.Parse(t.Key.Replace("w", "", StringComparison.OrdinalIgnoreCase), CultureInfo.InvariantCulture)));

                            var videoMetadata = new SwarmVideoMetadata(
                                sourceMetadata.Id,
                                sourceMetadata.Title,
                                sourceMetadata.Description,
                                TimeSpan.FromSeconds(alreadyPresentVideo.LastValidManifest!.Duration),
                                sourceMetadata.OriginVideoQualityLabel);

                            var videoSwarmFile = alreadyPresentVideo.LastValidManifest.Sources.Select(v => new VideoSwarmFile(v.Size, v.Quality, v.Reference));

                            var video = new Video(videoMetadata, videoSwarmFile, thumbnailFiles);

                            // Upload new manifest.
                            var metadataVideo = await ManifestDto.BuildNewAsync(
                                video,
                                alreadyPresentVideo.LastValidManifest.BatchId,
                                userEthAddress,
                                appVersionService.CurrentVersion);
                            var updatedPermalinkHash = await videoUploaderService.UploadVideoManifestAsync(metadataVideo, pinVideos, offerVideos);

                            // Update on index.
                            await ethernaIndexClient.VideosClient.VideosPutAsync(
                                alreadyPresentVideo.IndexId,
                                updatedPermalinkHash);

                            importResult = VideoImportResultSucceeded.ManifestUpdated(
                                sourceMetadata,
                                alreadyPresentVideo.IndexId,
                                updatedPermalinkHash);
                        }
                    }

                    //else, full upload the new video on etherna
                    else
                    {
                        // Validate metadata.
                        if (sourceMetadata.Title.Length > ethernaIndexParameters.VideoTitleMaxLength)
                            throw new InvalidOperationException(
                                $"Error: Title too long, max length: {ethernaIndexParameters.VideoTitleMaxLength}");
                        if (sourceMetadata.Description.Length > ethernaIndexParameters.VideoDescriptionMaxLength)
                            throw new InvalidOperationException(
                                $"Error: Description too long, max length: {ethernaIndexParameters.VideoDescriptionMaxLength}");

                        // Get and encode video from source.
                        var video = await videoProvider.GetVideoAsync(sourceMetadata);
                        video.EthernaIndexId = alreadyPresentVideo?.IndexId;

                        if (!video.EncodedFiles.Any())
                            throw new InvalidOperationException("Error: can't get valid stream from source");

                        // Upload video and all related data.
                        await videoUploaderService.UploadVideoAsync(video, pinVideos, offerVideos, userEthAddress);

                        importResult = VideoImportResultSucceeded.FullUploaded(
                            sourceMetadata,
                            video.EthernaIndexId!,
                            video.EthernaPermalinkHash!);
                    }

                    // Import succeeded.
                    ioService.WriteSuccessLine($"#{i + 1} Video imported successfully");
                }
                catch (Exception ex)
                {
                    importResult = new VideoImportResultFailed(sourceMetadata, ex);
                    
                    ioService.WriteErrorLine($"Exception importing video {sourceMetadata.Id}");
                    ioService.PrintException(ex);
                }
                finally
                {
                    try
                    {
                        // Clear tmp folder.
                        foreach (var file in CommonConsts.TempDirectory.GetFiles())
                            file.Delete();
                        foreach (var dir in CommonConsts.TempDirectory.GetDirectories())
                            dir.Delete(true);
                    }
                    catch (Exception e)
                    {
                        ioService.WriteErrorLine(
                            $"Warning: unable to delete some files in \"{CommonConsts.TempDirectory.FullName}\".");
                        ioService.PrintException(e);
                    }
                }
                
                // Report import result.
                results.Add(importResult);
                try
                {
                    /*
                     * Report etherna references even if video is already present on index.
                     * This handle cases where references are missing for some previous execution error.
                     */
                    await resultReporterService.ReportResultAsync(importResult);
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine("Unable to report etherna import result");
                    ioService.PrintException(ex);
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
            
            // Flush result report.
            await resultReporterService.FlushResultOutputAsync();

            // Print summary.
            ioService.WriteSuccessLine(
                $"""
                 Import completed.
                 
                 Total video processed: {results.Count}
                 Total video imported: {results.OfType<VideoImportResultSucceeded>().Count(r => r is { IsManifestUploaded: true, IsContentUploaded: true })}
                 Total video updated: {results.OfType<VideoImportResultSucceeded>().Count(r => r is { IsManifestUploaded: true, IsContentUploaded: false })}
                 Total video skipped (already present): {results.OfType<VideoImportResultSucceeded>().Count(r => r is { IsManifestUploaded: false, IsContentUploaded: false })}
                 Total video with errors: {results.OfType<VideoImportResultFailed>().Count()}
                 Total video deleted for missing in source: {importSummaryModelView.TotDeletedRemovedFromSource}
                 Total video deleted because not from this tool: {importSummaryModelView.TotDeletedExogenous}
                 """);
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
            } while (page.Elements.Count != 0);

            return videos.Select(v => new IndexedVideo(v));
        }
    }
}
