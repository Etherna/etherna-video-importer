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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private static readonly string[] ApiScopes = ["userApi.gateway", "userApi.index"];
        private static readonly string HelpText = $$"""
            Usage:  evi COMMAND SOURCE_URI [SOURCE_URI, ...] [OPTIONS]

            Commands:
              json      Import from json video list (requires metadata descriptor, see below)
              youtube   Import from multiple YouTube links. Supports videos, channels and playlists urls

            General Options:
              -k, --api-key           Api Key (optional)
              -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
              -i, --ignore-update     Ignore new version of EthernaVideoImporter
              -a, --auto-purchase     Accept automatically purchase of all batches
              -w, --write-file        Write published videos result to a JSON file

            Video Management Options:
              -t, --ttl               TTL (days) Postage Stamp (default: {{VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays}} days)
              -o, --offer             Offer video downloads to everyone
              --no-pin                Don't pin videos (pinning by default)
              --force                 Force upload video if they already have been uploaded
              -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
              --remove-unrecognized   Remove indexed videos not generated with this tool
              -u, --unpin             Try to unpin contents removed from index
              -c, --preset-codec      Preset of codec used for encoder (see ffmpeg documentation). Default: {{FFmpegServiceOptions.DefaultPresetCodec}}

            Bee Node Options:
              --bee-node              Use bee native node
              --bee-url               URL of Bee node (default: {{CommonConsts.BeeNodeUrl}})
              --bee-api-port          Port used by API (default: {{CommonConsts.BeePort}})

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
            List<string> sourceUrls = new();

            string? apiKey = null;
            string? customFFMpegFolderPath = null;
            bool ignoreUpdate = false;
            bool autoPurchaseBatches = false;
            string? outputFile = null;

            string? ttlPostageBatchStr = null;
            bool fundVideosDownload = false;
            bool fundVideosPin = true;
            bool forceVideoUpload = false;
            bool removeMissingVideosFromSource = false;
            bool removeUnrecognizedVideos = false;
            bool defundRemovedVideosPin = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            FFmpegH264Preset presetCodec = FFmpegServiceOptions.DefaultPresetCodec;

            bool useBeeNativeNode = false;
            string beeNodeUrl = CommonConsts.BeeNodeUrl;
            string? beeNodeApiPortStr = null;

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

                case "youtube":
                    sourceType = SourceType.YouTube;
                    break;

                default:
                    Console.WriteLine($"Invalid command: {args[0]}");
                    throw new ArgumentException($"Invalid command: {args[0]}");
            }

            //source uri and options
            var optArgs = args[1..];
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
                    
                    case "-w":
                    case "--write-file":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Output file path is missing");
                        outputFile = optArgs[++i];
                        break;

                    //video management
                    case "-t":
                    case "--ttl":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("TTL value is missing");
                        ttlPostageBatchStr = optArgs[++i];
                        break;

                    case "-o":
                    case "--offer":
                        fundVideosDownload = true;
                        break;

                    // case "-p":
                    // case "--pin":
                    //     pinVideos = true;
                    //     break;
                    case "--no-pin":
                        fundVideosPin = false;
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
                        defundRemovedVideosPin = true;
                        break;

                    case "-c":
                    case "--preset-codec":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Preset Codec value is missing");
                        presetCodec = Enum.Parse<FFmpegH264Preset>(optArgs[++i]);
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
                        if (!beeNodeUrl.EndsWith('/'))
                            beeNodeUrl += "/";
                        break;

                    case "--bee-api-port":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Bee node API port missing");
                        beeNodeApiPortStr = optArgs[++i];
                        useBeeNativeNode = true;
                        break;

                    default:
                        if (sourceType == SourceType.JsonList && sourceUrls.Count != 0)
                            throw new ArgumentException("Json import only supports a single url");
                        
                        sourceUrls.Add(optArgs[i]);
                        break;
                }
            }

            // Input validation.
            //bee node api port
            int beeNodeApiPort = CommonConsts.BeePort;
            if (!string.IsNullOrEmpty(beeNodeApiPortStr) &&
                !int.TryParse(beeNodeApiPortStr, CultureInfo.InvariantCulture, out beeNodeApiPort))
            {
                Console.WriteLine($"Invalid value for Gateway API port");
                return;
            }

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
                    ApiScopes,
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
                    ApiScopes,
                    HttpClientName,
                    c =>
                    {
                        c.Timeout = TimeSpan.FromMinutes(30);
                    });
            }
            ethernaClientsBuilder.AddEthernaGatewayClient(new Uri(CommonConsts.EthernaGatewayUrl))
                                 .AddEthernaIndexClient(new Uri(CommonConsts.EthernaIndexUrl));

            // Setup DI.
            //core
            services.AddCoreServices(
                encoderOptions =>
                {
                    encoderOptions.IncludeAudioTrack = includeAudioTrack;
                },
                ffMpegOptions =>
                {
                    ffMpegOptions.CustomFFmpegFolderPath = customFFMpegFolderPath;
                    ffMpegOptions.PresetCodec = presetCodec;
                },
                uploaderOptions =>
                {
                    uploaderOptions.AcceptPurchaseOfAllBatches = autoPurchaseBatches;

                    if (!string.IsNullOrEmpty(ttlPostageBatchStr))
                    {
                        if (int.TryParse(ttlPostageBatchStr, CultureInfo.InvariantCulture, out var ttlPostageStamp))
                            uploaderOptions.TtlPostageStamp = TimeSpan.FromDays(ttlPostageStamp);
                        else
                            throw new ArgumentException($"Invalid value for TTL Postage Stamp");
                    }
                },
                useBeeNativeNode);

            //source provider
            switch (sourceType)
            {
                case SourceType.JsonList:
                    //options
                    services.Configure<JsonListVideoProviderOptions>(options =>
                    {
                        options.JsonMetadataUri = new SourceUri(sourceUrls.First(), SourceUriKind.Local | SourceUriKind.OnlineAbsolute);
                    });
                    services.AddSingleton<IValidateOptions<JsonListVideoProviderOptions>, JsonListVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IVideoProvider, JsonListVideoProvider>();
                    break;
                case SourceType.YouTube:
                    //options
                    services.Configure<YouTubeVideoProviderOptions>(options =>
                    {
                        options.SourceUrls = sourceUrls;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeVideoProviderOptions>, YouTubeVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IYoutubeClient, YoutubeClient>();
                    services.AddTransient<IYoutubeDownloader, YoutubeDownloader>();
                    services.AddTransient<IVideoProvider, YouTubeVideoProvider>();
                    break;
                default:
                    throw new InvalidOperationException();
            }
            
            //result reporter
            services.Configure<JsonResultReporterOptions>(options =>
            {
                options.OutputFilePath = outputFile;
            });
            services.AddTransient<IResultReporterService, JsonResultReporterService>();

            var serviceProvider = services.BuildServiceProvider();

            // Start importer.
            var importer = serviceProvider.GetRequiredService<IEthernaVideoImporter>();
            await importer.RunAsync(
                removeUnrecognizedVideos,
                removeMissingVideosFromSource,
                forceVideoUpload,
                fundVideosDownload,
                fundVideosPin,
                defundRemovedVideosPin,
                ignoreUpdate);
        }
    }
}
