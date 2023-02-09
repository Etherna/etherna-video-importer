using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public class Runner
    {
        // Fields.
        private readonly ICleanerVideoService cleanerVideoService;
        private readonly IEthernaUserClientsAdapter ethernaClientService;
        private readonly ILinkReporterService linkReporterService;
        private readonly IVideoDownloaderService videoDownloaderService;
        private readonly IVideoUploaderService videoUploaderService;
        private readonly IVideoParseServices videoParseServices;

        // Constractor.
        public Runner(
            ICleanerVideoService cleanerVideoService,
            IEthernaUserClientsAdapter ethernaClientService,
            ILinkReporterService linkReporterService,
            IVideoDownloaderService videoDownloaderService,
            IVideoUploaderService videoUploaderService,
            IVideoParseServices videoParseServices)
        {
            if (videoParseServices is null)
                throw new ArgumentNullException(nameof(videoParseServices));
            if (linkReporterService is null)
                throw new ArgumentNullException(nameof(linkReporterService));
            if (cleanerVideoService is null)
                throw new ArgumentNullException(nameof(cleanerVideoService));
            if (ethernaClientService is null)
                throw new ArgumentNullException(nameof(ethernaClientService));
            if (videoDownloaderService is null)
                throw new ArgumentNullException(nameof(videoDownloaderService));
            if (videoUploaderService is null)
                throw new ArgumentNullException(nameof(videoUploaderService));

            this.cleanerVideoService = cleanerVideoService;
            this.linkReporterService = linkReporterService;
            this.ethernaClientService = ethernaClientService;
            this.videoDownloaderService = videoDownloaderService;
            this.videoUploaderService = videoUploaderService;
            this.videoParseServices = videoParseServices;
        }

        // Public methods.
        public async Task RunAsync(
            string sourceUri,
            bool offerVideo, 
            bool pinVideo, 
            bool deleteOldVideo,
            bool deleteInvalidVideo,
            string userEthAddr,
            string tmpFolderFullPath)
        {
            // Read from files md.
            Console.WriteLine($"Generate data info from {sourceUri}");
            var allVideoMinimalInfos = await videoParseServices.ToVideoDataMinimalInfoDtosAsync(sourceUri).ConfigureAwait(false);
            List<VideoData> allVideoDataInfos = new();

            // User video imported
            var importedVideos = await ethernaClientService.GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);

            // Import each video.
            var indexParams = await ethernaClientService.GetSystemParametersAsync().ConfigureAwait(false);
            var videoCount = 0;
            var totalVideo = allVideoMinimalInfos.Count();
            foreach (var videoMinimal in allVideoMinimalInfos)
            {
                try
                {
                    // Take all video info.
                    var video = await videoParseServices.ToVideoDataDtosAsync(videoMinimal.Uri).ConfigureAwait(false);
                    if (video is null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Unable to get data from {videoMinimal.Uri}");
                        Console.ResetColor();
                        continue;
                    }
                    allVideoDataInfos.Add(video);

                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{++videoCount} of #{totalVideo}");
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
                    var lastValidManifest = await ethernaClientService.GetLastValidManifestAsync(indexVideoId).ConfigureAwait(false);
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
                        await ethernaClientService.UpsertManifestToIndex(hashMetadataReference, video).ConfigureAwait(false);
                    }

                    // Save MD file with etherna values.
                    Console.WriteLine($"Save etherna values in file {video.MdFilepath}\n");
                    await linkReporterService.SetEthernaFieldsAsync(
                        video.EthernaIndex!,
                        video.EthernaPermalink!).ConfigureAwait(false);

                    // Import completed.
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"#{videoCount} Video imported successfully");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n#{videoCount} Video unable to import\n");
                    Console.ResetColor();
                }
                finally
                {
                    try
                    {
                        // Clear tmp folder.
                        var di = new DirectoryInfo(tmpFolderFullPath);
                        foreach (var file in di.GetFiles().OrderBy(file => file.CreationTime))
                            file.Delete();
                        foreach (var dir in di.GetDirectories().OrderBy(file => file.CreationTime))
                            dir.Delete(true);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Warning: Unable to clear some file insede of {tmpFolderFullPath} please remove manually\n");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine($"Done.");
            }

            // Delete old video.
            if (deleteOldVideo)
            {
                await cleanerVideoService.RunOldDeleterAsync().ConfigureAwait(false);
            }

            // User video.
            if (deleteInvalidVideo)
            {
                await cleanerVideoService.RunCleanerAsync(allVideoDataInfos).ConfigureAwait(false);
            }
        }
    }
}
