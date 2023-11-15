//   Copyright 2022-present Etherna SA
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

using Etherna.Sdk.Users.Native;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Options;
using Etherna.VideoImporter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private static readonly string HelpText = $$"""
            Usage:  evi COMMAND SOURCE_URI [OPTIONS]

            Commands:
              json              Import from json video list (requires metadata descriptor, see below)
              youtube-channel   Import from a YouTube channel
              youtube-video     Import from a YouTube video

            General Options:
              -k, --api-key           Api Key (optional)
              -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
              -i, --ignore-update     Ignore new version of EthernaVideoImporter
              -a, --auto-purchase     Accept automatically purchase of all batches

            Video Management Options:
              -t, --ttl               TTL (days) Postage Stamp (default: {{VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays}} days)
              -o, --offer             Offer video downloads to everyone
              --no-pin                Don't pin videos (pinning by default)
              --force                 Force upload video if they already have been uploaded
              -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
              --remove-unrecognized   Remove indexed videos not generated with this tool
              -u, --unpin             Try to unpin contents removed from index

            Bee Node Options:
              --bee-node              Use bee native node
              --bee-url               URL of Bee node (default: {{CommonConsts.BeeNodeUrl}})
              --bee-api-port          Port used by API (default: {{CommonConsts.BeeApiPort}})
              --bee-debug-port        Port used by Debug (default: {{CommonConsts.BeeDebugPort}})

            Json videos metadata format:
            To import from a video list you need a metadata descriptor file. Metadata is a JSON file with the following structure:

            [
                {
                    "Id": "myId1",
                    "Title": "First video title",
                    "Description": "My first video description",
                    "VideoFilePath": "path/to/your/video1.mp4",
                    "ThumbnailFilePath": "path/to/your/optional/thumbnail1.jpg",
                    "OldIds": [
                        "optionalOldId1",
                        "optionalOldId2"
                    ]
                },
                {
                    "Id": "myId2",
                    "Title": "Second video title",
                    "Description": "My second video description",
                    "VideoFilePath": "http://example.com/stream.m3u8",
                    "ThumbnailFilePath": "path/to/your/optional/thumbnail2.jpg"
                },
                ...
            ]

            Id field is mandatory, and is needed to trace same video through different executions. Each Id needs to be unique.

            Run 'evi -h' or 'evi --help' to print help.
            """;
        private const string HttpClientName = "ethernaAuthnHttpClient";

        // Methods.
        static async Task Main(string[] args)
        {
            // Parse arguments.
            SourceType sourceType;
            string sourceUri;

            string? apiKey = null;
            string? customFFMpegFolderPath = null;
            bool ignoreUpdate = false;
            bool autoPurchaseBatches = false;

            string? ttlPostageStampStr = null;
            bool offerVideos = false;
            bool pinVideos = true;
            bool forceVideoUpload = false;
            bool removeMissingVideosFromSource = false;
            bool removeUnrecognizedVideos = false;
            bool unpinRemovedVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21

            bool useBeeNativeNode = false;
            string beeNodeUrl = CommonConsts.BeeNodeUrl;
            string? beeNodeApiPortStr = null;
            string? beeNodeDebugPortStr = null;

            //print help
            if (args.Length == 0)
            {
                Console.WriteLine(HelpText);
                return;
            }
            else if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "-h":
                    case "--help":
                        Console.WriteLine(HelpText);
                        return;

                    default:
                        throw new ArgumentException("Invalid number of arguments");
                }
            }

            //command
            switch (args[0])
            {
                case "json":
                    sourceType = SourceType.JsonList;
                    break;

                case "youtube-channel":
                    sourceType = SourceType.YouTubeChannel;
                    break;

                case "youtube-video":
                    sourceType = SourceType.YouTubeVideo;
                    break;

                default:
                    Console.WriteLine($"Invalid command: {args[0]}");
                    throw new ArgumentException($"Invalid command: {args[0]}");
            }

            //source uri
            sourceUri = args[1];

            //options
            var optArgs = args[2..];
            for (int i = 0; i < optArgs.Length; i++)
            {
                switch (optArgs[i])
                {
                    //general
                    case "-k":
                    case "--api-key":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Api Key is missing");
                        apiKey = optArgs[++i];
                        break;

                    case "-f":
                    case "--ffmpeg-path":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("FFmpeg folder is missing");
                        customFFMpegFolderPath = optArgs[++i];
                        break;

                    case "-i":
                    case "--ignore-update":
                        ignoreUpdate = true;
                        break;

                    case "-a":
                    case "--auto-purchase":
                        autoPurchaseBatches = true;
                        break;

                    //video management
                    case "-t":
                    case "--ttl":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("TTL value is missing");
                        ttlPostageStampStr = optArgs[++i];
                        break;

                    case "-o":
                    case "--offer":
                        offerVideos = true;
                        break;

                    // case "-p":
                    // case "--pin":
                    //     pinVideos = true;
                    //     break;
                    case "--no-pin":
                        pinVideos = false;
                        break;

                    case "--force":
                        forceVideoUpload = true;
                        break;

                    case "-m":
                    case "--remove-missing":
                        removeMissingVideosFromSource = true;
                        break;

                    case "--remove-unrecognized":
                        removeUnrecognizedVideos = true;
                        break;

                    case "-u":
                    case "--unpin":
                        unpinRemovedVideos = true;
                        break;

                    //bee node
                    case "--bee-node":
                        useBeeNativeNode = true;
                        break;

                    case "--bee-url":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Bee node value is missing");
                        beeNodeUrl = optArgs[++i];
                        useBeeNativeNode = true;
                        if (!beeNodeUrl.EndsWith("/", StringComparison.InvariantCulture))
                            beeNodeUrl += "/";
                        break;

                    case "--bee-api-port":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Bee node API port missing");
                        beeNodeApiPortStr = optArgs[++i];
                        useBeeNativeNode = true;
                        break;

                    case "--bee-debug-port":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Bee node Debug port missing");
                        beeNodeDebugPortStr = optArgs[++i];
                        useBeeNativeNode = true;
                        break;

                    default:
                        throw new ArgumentException(optArgs[i] + " is not a valid option");
                }
            }

            // Input validation.
            //offer video
            if (offerVideos && useBeeNativeNode)
            {
                Console.WriteLine($"Only Etherna Gateway supports offering video downloads to everyone");
                return;
            }

            //bee node api port
            int beeNodeApiPort = CommonConsts.BeeApiPort;
            if (!string.IsNullOrEmpty(beeNodeApiPortStr) &&
                !int.TryParse(beeNodeApiPortStr, CultureInfo.InvariantCulture, out beeNodeApiPort))
            {
                Console.WriteLine($"Invalid value for Gateway API port");
                return;
            }

            //bee node debug port
            int beeNodeDebugPort = CommonConsts.BeeDebugPort;
            if (!string.IsNullOrEmpty(beeNodeDebugPortStr) &&
                !int.TryParse(beeNodeDebugPortStr, CultureInfo.InvariantCulture, out beeNodeDebugPort))
            {
                Console.WriteLine($"Invalid value for Gateway Debug port");
                return;
            }

            //deny delete video old sources when is single video
            if (sourceType == SourceType.YouTubeVideo && removeMissingVideosFromSource)
            {
                Console.WriteLine($"Cannot delete video removed from source when the source is a single video");
                return;
            }

            // Check for new versions.
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync();
            if (newVersionAvaiable && !ignoreUpdate)
                return;

            // Register etherna service clients.
            var services = new ServiceCollection();
            IEthernaUserClientsBuilder ethernaClientsBuilder;
            if (apiKey is null) //"code" grant flow
            {
                ethernaClientsBuilder = services.AddEthernaUserClientsWithCodeAuth(
                    CommonConsts.EthernaSsoUrl,
                    CommonConsts.EthernaVideoImporterClientId,
                    null,
                    11420,
                    new[] { "userApi.gateway", "userApi.index" },
                    HttpClientName,
                    c =>
                    {
                        c.Timeout = TimeSpan.FromMinutes(30);
                    });
            }
            else //"password" grant flow
            {
                ethernaClientsBuilder = services.AddEthernaUserClientsWithApiKeyAuth(
                    CommonConsts.EthernaSsoUrl,
                    apiKey,
                    new[] { "userApi.gateway", "userApi.index" },
                    HttpClientName,
                    c =>
                    {
                        c.Timeout = TimeSpan.FromMinutes(30);
                    });
            }
            ethernaClientsBuilder.AddEthernaGatewayClient(new Uri(CommonConsts.EthernaGatewayUrl))
                                 .AddEthernaIndexClient(new Uri(CommonConsts.EthernaIndexUrl));

            // Setup DI.
            services.AddCoreServices(
                encoderOptions =>
                {
                    encoderOptions.IncludeAudioTrack = includeAudioTrack;
                },
                ffMpegOptions =>
                {
                    ffMpegOptions.CustomFFmpegFolderPath = customFFMpegFolderPath;
                },
                uploaderOptions =>
                {
                    uploaderOptions.AcceptPurchaseOfAllBatches = autoPurchaseBatches;

                    if (!string.IsNullOrEmpty(ttlPostageStampStr))
                    {
                        if (int.TryParse(ttlPostageStampStr, CultureInfo.InvariantCulture, out var ttlPostageStamp))
                            uploaderOptions.TtlPostageStamp = TimeSpan.FromDays(ttlPostageStamp);
                        else
                            throw new ArgumentException($"Invalid value for TTL Postage Stamp");
                    }
                },
                HttpClientName,
                useBeeNativeNode);

            switch (sourceType)
            {
                case SourceType.JsonList:
                    //options
                    services.Configure<JsonListVideoProviderOptions>(options =>
                    {
                        options.JsonMetadataUri = new SourceUri(sourceUri, SourceUriKind.Local | SourceUriKind.OnlineAbsolute);
                    });
                    services.AddSingleton<IValidateOptions<JsonListVideoProviderOptions>, JsonListVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IVideoProvider, JsonListVideoProvider>();
                    break;
                case SourceType.YouTubeChannel:
                    //options
                    services.Configure<YouTubeChannelVideoProviderOptions>(options =>
                    {
                        options.ChannelUrl = sourceUri;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeChannelVideoProviderOptions>, YouTubeChannelVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IYoutubeClient, YoutubeClient>();
                    services.AddTransient<IYoutubeDownloader, YoutubeDownloader>();
                    services.AddTransient<IVideoProvider, YouTubeChannelVideoProvider>();
                    break;
                case SourceType.YouTubeVideo:
                    //options
                    services.Configure<YouTubeSingleVideoProviderOptions>(options =>
                    {
                        options.VideoUrl = sourceUri;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeSingleVideoProviderOptions>, YouTubeSingleVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IYoutubeClient, YoutubeClient>();
                    services.AddTransient<IYoutubeDownloader, YoutubeDownloader>();
                    services.AddTransient<IVideoProvider, YouTubeSingleVideoProvider>();
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Start importer.
            var importer = serviceProvider.GetRequiredService<IEthernaVideoImporter>();
            await importer.RunAsync(
                removeUnrecognizedVideos,
                removeMissingVideosFromSource,
                forceVideoUpload,
                offerVideos,
                pinVideos,
                unpinRemovedVideos);
        }
    }
}
