// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Authentication;
using Etherna.Authentication.Native;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ModelView;
using Etherna.VideoImporter.Core.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Video = Etherna.VideoImporter.Core.Models.Domain.Video;

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
        private readonly IHasher hasher;
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
            IHasher hasher,
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
            this.hasher = hasher;
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
            bool fundDownload,
            bool fundPinning,
            bool unpinRemovedVideos,
            bool ignoreAppUpdates)
        {
            // Startup operations.
            PrintStartupHeader();
            
            var newVersionAvailable = await CheckNewAppVersionsAsync();
            if (newVersionAvailable && !ignoreAppUpdates)
                return;
            
            await SignInUserAsync();

            // Get info from authenticated user.
            var userEthAddress = await ethernaOpenIdConnectClient.GetEtherAddressAsync();
            var userName = await ethernaOpenIdConnectClient.GetUsernameAsync();

            ioService.WriteLine($"User {userName} authenticated");

            // Get videos info.
            ioService.WriteLine($"Get videos metadata from {videoProvider.SourceName}");

            var sourceVideosMetadata = await videoProvider.GetVideosMetadataAsync();
            var totalSourceVideo = sourceVideosMetadata.Length;

            ioService.WriteLine($"Found {totalSourceVideo} valid distinct videos from source");

            // Get information from etherna index.
            ioService.WriteLine("Reading user videos from etherna index... (it may take a while)");

            var userVideosOnIndex = await ethernaIndexClient.GetAllVideosByOwnerAsync(
                userEthAddress,
                iv =>
                {
                    ioService.WriteLine(iv.LastValidManifest?.Manifest is null
                        ? "    Found video with invalid manifest"
                        : $"    Found video: \"{iv.LastValidManifest.Manifest.Title}\"");
                });
            var ethernaIndexParameters = await ethernaIndexClient.GetIndexParametersAsync();

            ioService.WriteLine($"Found {userVideosOnIndex.Length} videos already published on etherna index");

            // Import each video.
            ioService.WriteLine("Start importing videos");

            var importSummaryModelView = new ImportSummaryModelView();
            var results = new List<VideoImportResultBase>();
            foreach (var (sourceMetadata, i) in sourceVideosMetadata.Select((m, i) => (m, i)))
            {
                VideoImportResultBase? importResult = null;

                try
                {
                    ioService.WriteLine("===============================", false);
                    ioService.WriteLine($"Processing video #{i + 1} of #{totalSourceVideo}. Source Id: {sourceMetadata.SourceId}");

                    if (!sourceMetadata.IsDataFetched &&
                        !await sourceMetadata.TryFetchMetadataAsync(ioService))
                        throw new InvalidOperationException("Can't fetch source metadata");

                    ioService.WriteLine($"Title: {sourceMetadata.Title}", false);

                    // Search already uploaded video. Compare serialized id on manifest personal data with metadata id from source.
                    var allVideoIdHashes = sourceMetadata.AllSourceIds
                        .Select(id => hasher.ComputeHash(id).ToHex());
                    
                    var alreadyIndexedVideo = userVideosOnIndex.FirstOrDefault(
                        indexedVideo => indexedVideo.LastValidManifest?.Manifest?.PersonalData?.SourceVideoId is not null &&
                                        indexedVideo.LastValidManifest.Manifest.PersonalData.SourceProviderName == videoProvider.SourceName &&
                                        allVideoIdHashes.Contains(indexedVideo.LastValidManifest.Manifest.PersonalData.SourceVideoId));
                    
                    // Try to minimize operations as much as possible.
                    if (alreadyIndexedVideo is not null && !forceVideoUpload)
                    {
                        ioService.WriteLine("Video already uploaded on etherna");
                        
                        // Check minimal required operation.
                        var requiredOperation = migrationService.DecideOperation(
                            alreadyIndexedVideo,
                            sourceMetadata);

                        switch (requiredOperation)
                        {
                            case OperationType.Skip:
                                ioService.WriteLine($"Metadata hasn't changed, skip the import");

                                importResult = VideoImportResultSucceeded.Skipped(
                                    sourceMetadata,
                                    alreadyIndexedVideo.Id,
                                    alreadyIndexedVideo.LastValidManifest!.Hash);
                                break;
                            
                            case OperationType.UpdateManifest:
                                ioService.WriteLine($"Metadata has changed, update the video manifest");

                                var updatedPermalinkHash = await TryMigrateManifestAsync(
                                    alreadyIndexedVideo,
                                    sourceMetadata,
                                    userEthAddress,
                                    fundDownload,
                                    fundPinning);

                                if (updatedPermalinkHash != null)
                                    importResult = VideoImportResultSucceeded.ManifestUpdated(
                                        sourceMetadata,
                                        alreadyIndexedVideo.Id,
                                        updatedPermalinkHash.Value);
                                break;
                        }
                    }
                    
                    //else, full upload the new video on etherna
                    if(importResult is null)
                    {
                        // Validate metadata.
                        if (sourceMetadata.Title.Length > ethernaIndexParameters.VideoTitleMaxLength)
                            throw new InvalidOperationException(
                                $"Error: Title too long, max length: {ethernaIndexParameters.VideoTitleMaxLength}");
                        if (sourceMetadata.Description.Length > ethernaIndexParameters.VideoDescriptionMaxLength)
                            throw new InvalidOperationException(
                                $"Error: Description too long, max length: {ethernaIndexParameters.VideoDescriptionMaxLength}");

                        // Get and encode video from source.
                        var video = await videoProvider.BuildVideoFromMetadataAsync(sourceMetadata);
                        video.EthernaIndexId = alreadyIndexedVideo?.Id;

                        // Upload video and all related data.
                        await videoUploaderService.UploadVideoAsync(video, fundPinning, fundDownload, userEthAddress);

                        importResult = VideoImportResultSucceeded.FullUploaded(
                            sourceMetadata,
                            video.EthernaIndexId!,
                            video.EthernaPermalinkHash!.Value);
                    }

                    // Import succeeded.
                    ioService.WriteSuccessLine($"#{i + 1} Video imported successfully");
                }
                catch (Exception ex)
                {
                    importResult = new VideoImportResultFailed(sourceMetadata, ex);
                    
                    ioService.WriteErrorLine($"Exception importing video {sourceMetadata.SourceId}");
                    ioService.PrintException(ex);
                }
                finally
                {
                    try
                    {
                        // Clear tmp folder for next import.
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
            
            // Delete temporary directory.
            CommonConsts.TempDirectory.Delete(true);

            // Clean up user channel on etherna index.
            if (removeMissingVideosFromSource)
                importSummaryModelView.TotDeletedRemovedFromSource = await cleanerVideoService.DeleteVideosRemovedFromSourceAsync(
                    sourceVideosMetadata,
                    userVideosOnIndex,
                    unpinRemovedVideos,
                    videoProvider.SourceName);

            if (removeUnrecognizedVideos)
                importSummaryModelView.TotDeletedExogenous = await cleanerVideoService.DeleteExogenousVideosAsync(
                    userVideosOnIndex,
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
        private async Task<bool> CheckNewAppVersionsAsync()
        {
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

                    return true;
                }
            }
            catch (Exception e)
            {
                ioService.WriteErrorLine("Unable to verify last version on GitHub");
                ioService.PrintException(e);
            }

            return false;
        }
        
        private void PrintStartupHeader()
        {
            ioService.WriteLine(
                $"""

                 Etherna Video Importer (v{appVersionService.CurrentVersion})

                 """, false);
        }

        private async Task SignInUserAsync()
        {
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
        }

        private async Task<SwarmHash?> TryMigrateManifestAsync(
            IndexedVideo alreadyIndexedVideo,
            VideoMetadataBase sourceMetadata,
            string userEthAddress,
            bool fundDownload,
            bool fundPinning)
        {
            if (alreadyIndexedVideo.LastValidManifest?.Manifest is null)
                return null;
            
            Video video;
            try
            {
                // Download thumbnail.
                List<ThumbnailFile> thumbnailFiles = [];
                foreach (var thumbnailSource in alreadyIndexedVideo.LastValidManifest.Manifest.Thumbnail.Sources)
                    thumbnailFiles.Add(await migrationService.DownloadThumbnailFile(
                        alreadyIndexedVideo.LastValidManifest.Hash,
                        thumbnailSource.Uri));

                // Download encoded video.
                var videoEncoding = await migrationService.DownloadVideoEncodingFromManifestAsync(
                    alreadyIndexedVideo.LastValidManifest.Hash,
                    alreadyIndexedVideo.LastValidManifest.Manifest);
                
                video = new Video(
                    sourceMetadata,
                    [],
                    thumbnailFiles.ToArray(),
                    videoEncoding)
                {
                    EthernaIndexId = alreadyIndexedVideo.Id
                };
            }
            catch { return null; }

            // Upload new manifest.
            await videoUploaderService.UploadVideoAsync(
                video,
                fundPinning,
                fundDownload,
                userEthAddress,
                alreadyIndexedVideo.LastValidManifest.Manifest.BatchId);

            return video.EthernaPermalinkHash;
        }
    }
}
