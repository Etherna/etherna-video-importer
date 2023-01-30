using Etherna.BeeNet;
using Etherna.EthernaVideoImporter.Dtos;
using Etherna.EthernaVideoImporter.Models;
using Etherna.EthernaVideoImporter.Services;
using Etherna.EthernaVideoImporter.SSO;
using Etherna.EthernaVideoImporter.Utilities;
using Etherna.ServicesClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode.Common;

namespace Etherna.EthernaVideoImporter
{
    internal static class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveVideoImporter help:\n\n" +
            "-md\tSource folder path with *.md files to import\n" +
            "-yc\tYoutube channel url\n" +
            "-yv\tYoutube single video url\n" +
            "-f\tFree video offer by creator\n" +
            "-p\tPin video\n" +
            "-d\tDelete old videos that are no longer in the .MD files\n" +
            "-c\tDelete all index video with no valid manifest or old PersonalData\n" +
            "-ff\tPath FFmpeg (default dir: FFmpeg\\)\n" +
            "\n" +
            "-h\tPrint help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? folderFilesMDPath = null;
            string? youtubeChannelUrl = null;
            string? youtubeVideoUrl = null;
            string? ffMpegFolderPath = null;
            bool offerVideo = false;
            bool pinVideo = false;
            bool deleteOldVideo = false;
            bool deleteInvalidVideo = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-md": folderFilesMDPath = args[++i]; break;
                    case "-yc": youtubeChannelUrl = args[++i]; break;
                    case "-yv": youtubeVideoUrl = args[++i]; break;
                    case "-ff": ffMpegFolderPath = args[++i]; break;
                    case "-f": offerVideo = true; break;
                    case "-p": pinVideo = true; break;
                    case "-d": deleteOldVideo = true; break;
                    case "-c": deleteInvalidVideo = true; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }
            if (ffMpegFolderPath is not null &&
                !Directory.Exists(ffMpegFolderPath))
            {
                Console.WriteLine($"Path FFmpeg not found ({ffMpegFolderPath})");
                return;
            }
            ffMpegFolderPath ??= "FFmpeg\\";

            // Request missing params.
            var sourceParam = 0;
            sourceParam += string.IsNullOrWhiteSpace(folderFilesMDPath) ? 0 : 1;
            sourceParam += string.IsNullOrWhiteSpace(youtubeChannelUrl) ? 0 : 1;
            sourceParam += string.IsNullOrWhiteSpace(youtubeVideoUrl) ? 0 : 1;
            switch (sourceParam)
            {
                case 0:
                    Console.WriteLine("Missing require param (one of: -md or -ch or -yt)");
                    return;
                case > 1:
                    Console.WriteLine("Only one of require param (one of: -md or -ch or -yt)");
                    return;
            }

            // Check tmp folder.
            const string tmpFolder = "tmpData";
            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);

            // Sign with SSO and create auth client.
            var authResult = await SignServices.SigInSSO().ConfigureAwait(false);
            if (authResult.IsError)
            {
                Console.WriteLine($"Error during authentication");
                Console.WriteLine(authResult.Error);
                return;
            }
            var userEthAddr = authResult.User.Claims.Where(i => i.Type == "ether_address").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(userEthAddr))
            {
                Console.WriteLine($"Missing ether address");
                return;
            }
            using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };

            // Inizialize services.
            var ethernaUserClients = new EthernaUserClients(
                new Uri(CommonConst.ETHERNA_CREDIT),
                new Uri(CommonConst.ETHERNA_GATEWAY),
                new Uri(CommonConst.ETHERNA_INDEX),
                new Uri(CommonConst.SSO_AUTHORITY),
                () => httpClient);
            var ethernaClientService = new EthernaUserClientsAdapter(ethernaUserClients);
            using var videoDownloaderService = new VideoDownloaderService(ffMpegFolderPath, tmpFolderFullPath);
            var beeNodeClient = new BeeNodeClient(
                CommonConst.ETHERNA_GATEWAY,
                CommonConst.BEENODE_GATEWAYPORT,
                null,
                CommonConst.BEENODE_GATEWAYVERSION,
                CommonConst.BEENODE_DEBUGAVERSION,
                httpClient);
            var videoUploaderService = new VideoUploaderService(
                beeNodeClient,
                ethernaClientService,
                userEthAddr,
                httpClient);

            // Read from files md.
            //var allVideoMinimalInfos = MdVideoParserService.ToVideoDataMinimalInfoDtos(sourceFolderPath);
            var allVideoMinimalInfos = await YouTubeChannelVideoParserServices.ToVideoDataMinimalInfoDtosAsync("https://www.youtube.com/@PolkadotNetwork").ConfigureAwait(false);
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
                    //var video = MdVideoParserService.ToVideoDataDtos(videoMinimal.Uri);
                    var video = await YouTubeChannelVideoParserServices.ToVideoDataDtosAsync(videoMinimal.Uri).ConfigureAwait(false);
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
                    var sourceMdFile = new LinkReporterService(video.MdFilepath!);
                    await sourceMdFile.SetEthernaFieldsAsync(
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
            }

            // Delete old video.
            if (deleteOldVideo)
            {
                // Get video indexed
                var videoIds = importedVideos.Select(
                        videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                // Get video indexed but not in repository files *.md
                var removableIds = videoIds.Except(allVideoDataInfos.Select(repVideo => repVideo.YoutubeId).ToList());
                foreach (var videoId in removableIds)
                {
                    try
                    {
                        var itemToRemove = importedVideos.Where(
                        videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId == videoId)
                            .First();

                        await ethernaClientService.DeleteIndexVideoAsync(itemToRemove.Id).ConfigureAwait(false);

                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Video {itemToRemove.Id} removed");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {videoId}\n");
                        Console.ResetColor();
                    }
                }
            }

            // User video.
            if (deleteInvalidVideo)
            {
                foreach (var video in importedVideos)
                {
                    if (video.LastValidManifest is not null &&
                        !string.IsNullOrWhiteSpace(video.LastValidManifest.PersonalData))
                        continue;

                    try
                    {
                        await ethernaClientService.DeleteIndexVideoAsync(video.Id).ConfigureAwait(false);
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Video {video.Id} removed");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {video.Id}\n");
                        Console.ResetColor();
                    }
                }
            }
        }

        // Private helpers.
        private static string ReadStringIfEmpty(string? strValue)
        {
            if (string.IsNullOrWhiteSpace(strValue))
            {
                while (string.IsNullOrWhiteSpace(strValue))
                {
                    strValue = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(strValue))
                        Console.WriteLine("*Empty string not allowed*");
                }
            }
            else Console.WriteLine(strValue);

            return strValue;
        }


    }
}
