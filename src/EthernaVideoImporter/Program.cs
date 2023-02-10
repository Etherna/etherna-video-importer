﻿using Etherna.BeeNet;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.SSO;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Services;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private const int DefaultTTLPostageStamp = 365;
        private const string DefaultFFmpegFolder = @".\FFmpeg\";
        private static readonly string HelpText =
            "Etherna Video Importer help:\n\n" +
            "-yc\tYoutube channel url\n" +
            "-yv\tYoutube single video url\n" +
            "-f\tFree videos offered by creator\n" +
            "-p\tPin video\n" +
            "-d\tRemove old videos that are no longer in source channel\n" +
            "-c\tRemove present videos not uploaded with this tool from channel\n" +
            $"-ff\tPath FFmpeg (default dir: {DefaultFFmpegFolder})\n" +
            $"-t\tTTL (days) Postage Stamp (default value: {DefaultTTLPostageStamp} days)\n" +
            "\n" +
            "-h\tPrint help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? youtubeChannelUrl = null;
            string? youtubeVideoUrl = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteSourceRemovedVideos = false;
            bool deleteVideosFromOtherSources = false;
            bool includeAudioTrack = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-yc": youtubeChannelUrl = args[++i]; break;
                    case "-yv": youtubeVideoUrl = args[++i]; break;
                    case "-ff": ffMpegFolderPath = args[++i]; break;
                    case "-f": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-d": deleteSourceRemovedVideos = true; break;
                    case "-c": deleteVideosFromOtherSources = true; break;
                    case "-t": ttlPostageStampStr = args[++i]; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Input validation.
            //FFmpeg dir
            if (!Directory.Exists(ffMpegFolderPath))
            {
                Console.WriteLine($"FFmpeg not found at ({ffMpegFolderPath})");
                return;
            }

            //ttl postage batch
            if (!string.IsNullOrEmpty(ttlPostageStampStr) &&
                int.TryParse(ttlPostageStampStr, CultureInfo.InvariantCulture, out ttlPostageStamp))
            {
                Console.WriteLine($"Invalid value for TTL Postage Stamp");
                return;
            }

            //sources
            var sourceParam = 0;
            sourceParam += string.IsNullOrWhiteSpace(youtubeChannelUrl) ? 0 : 1;
            sourceParam += string.IsNullOrWhiteSpace(youtubeVideoUrl) ? 0 : 1;
            switch (sourceParam)
            {
                case 0:
                    Console.WriteLine("Missing required source param (one of: -ch or -yt)");
                    return;
                case > 1:
                    Console.WriteLine("Select only one source param (one of: -ch or -yt)");
                    return;
            }
            var sourceUri = youtubeChannelUrl ?? youtubeVideoUrl!;

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

            // Inizialize services.
            using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };
            var ethernaUserClients = new EthernaUserClients(
                new Uri(CommonConst.ETHERNA_CREDIT),
                new Uri(CommonConst.ETHERNA_GATEWAY),
                new Uri(CommonConst.ETHERNA_INDEX),
                new Uri(CommonConst.SSO_AUTHORITY),
                () => httpClient);
            var ethernaClientService = new EthernaUserClientsAdapter(ethernaUserClients);
            using var videoDownloaderService = new VideoDownloaderService(ffMpegFolderPath, tmpFolderFullPath, includeAudioTrack);
            using var beeNodeClient = new BeeNodeClient(
                CommonConst.ETHERNA_GATEWAY,
                CommonConst.BEENODE_GATEWAYPORT,
                null,
                CommonConst.BEENODE_GATEWAYVERSION,
                CommonConst.BEENODE_DEBUGVERSION,
                httpClient);
            var videoUploaderService = new VideoUploaderService(
                beeNodeClient,
                ethernaClientService,
                userEthAddr,
                httpClient,
                includeAudioTrack,
                ttlPostageStamp);

            // Call runner.
            var runner = new Runner(
                new CleanerVideoService(ethernaClientService, userEthAddr),
                ethernaClientService,
                new LinkReporterService(),
                videoDownloaderService,
                new YouTubeChannelVideoParserServices(),
                videoUploaderService);
            await runner.RunAsync(
                sourceUri,
                offerVideos,
                pinVideos,
                deleteSourceRemovedVideos,
                deleteVideosFromOtherSources,
                userEthAddr,
                tmpFolderFullPath).ConfigureAwait(false);
        }
    }
}
