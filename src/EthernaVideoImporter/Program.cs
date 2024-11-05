﻿// Copyright 2022-present Etherna SA
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

using Etherna.Sdk.Users;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.FFmpeg;
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
using System.Net;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private static readonly string[] ApiScopes = ["userApi.gateway", "userApi.index"];
        private static readonly string HelpText = $$"""
            Tool to import videos on Etherna from different sources.
            
                Program distributed under AGPLv3 license. Copyright since 2022 by Etherna SA.
                You can find source code at: https://github.com/Etherna/etherna-video-importer
            
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
              --dry                   Run in dry mode. Any action on swarm gateway or index is performed read-only
              --working-dir           Use custom working directory
              
            YouTube Options:
              --yt-autogen-sub        Import also YouTube autogenerated subtitles
              --yt-cookies            List of cookies to use with YouTube requests, divided by ';'

            Video Management Options:
              -t, --ttl               TTL (days) Postage Stamp (default: {{VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays}} days)
              -o, --offer             Offer video downloads to everyone
              --no-pin                Don't pin videos (pinning by default)
              --force                 Force upload video if they already have been uploaded
              -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
              --remove-unrecognized   Remove indexed videos not generated with this tool
              -u, --unpin             Try to unpin contents removed from index
              --bitrate-reduction     Reduce bitrate from HLS standard. [None, Low, Normal, High, Extreme, Insane]. Default: {{FFmpegServiceOptions.DefaultBitrateReduction}}
              --ffmpeg-preset         Preset option with ffmpeg (https://trac.ffmpeg.org/wiki/Encode/H.264#Preset). Default: {{FFmpegServiceOptions.DefaultFFmpegPreset}}

            Gateway Options:
              --bee-node              Use bee node as gateway
              --gateway-url           Connect gateway with custom URL

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
            bool isDryRun = false;
            string? customWorkingDirectory = null;

            bool importAutogeneratedSubtitles = false;
            string? ytCookies = null;

            string? ttlPostageBatchStr = null;
            bool fundVideosDownload = false;
            bool fundVideosPin = true;
            bool forceVideoUpload = false;
            bool removeMissingVideosFromSource = false;
            bool removeUnrecognizedVideos = false;
            bool defundRemovedVideosPin = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            var bitrateReduction = FFmpegBitrateReduction.Normal;
            var ffmpegPreset = FFmpegServiceOptions.DefaultFFmpegPreset;

            bool useBeeNativeNode = false;
            string? customGatewayUrl = null;

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
                    
                    case "--dry":
                        Console.WriteLine("Dry Run");
                        isDryRun = true;
                        break;
                    
                    case "--working-dir":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Working directory is missing");
                        customWorkingDirectory = optArgs[++i];
                        break;
                    
                    //youtube
                    case "--yt-autogen-sub":
                        importAutogeneratedSubtitles = true;
                        break;
                    
                    case "--yt-cookies":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("YT cookies are missing");
                        ytCookies = optArgs[++i];
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
                    
                    case "--bitrate-reduction":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Bitrate reduction value is missing");
                        bitrateReduction = Enum.Parse<FFmpegBitrateReduction>(optArgs[++i], true);
                        break;

                    case "--ffmpeg-preset":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Preset value is missing");
                        ffmpegPreset = Enum.Parse<FFmpegH264Preset>(optArgs[++i], true);
                        break;

                    //bee node
                    case "--bee-node":
                        useBeeNativeNode = true;
                        break;

                    case "--gateway-url":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Gateway url is missing");
                        customGatewayUrl = optArgs[++i];
                        if (!customGatewayUrl.EndsWith('/'))
                            customGatewayUrl += "/";
                        break;

                    default:
                        if (sourceType == SourceType.JsonList && sourceUrls.Count != 0)
                            throw new ArgumentException("Json import only supports a single url");
                        
                        sourceUrls.Add(optArgs[i]);
                        break;
                }
            }

            // Input validation.
            // Register etherna service clients.
            var services = new ServiceCollection();
            IEthernaUserClientsBuilder ethernaClientsBuilder;
            if (apiKey is null) //"code" grant flow
            {
                ethernaClientsBuilder = services.AddEthernaUserClientsWithCodeAuth(
                    CommonConsts.EthernaVideoImporterClientId,
                    null,
                    11420,
                    ApiScopes,
#if DEVENV
                    authority: "https://localhost:44379/",
#else
                    authority: EthernaUserClientsBuilder.DefaultSsoUrl,
#endif
                    httpClientName: HttpClientName,
                    configureHttpClient: c =>
                    {
                        c.Timeout = TimeSpan.FromMinutes(30);
                    });
            }
            else //"password" grant flow
            {
                ethernaClientsBuilder = services.AddEthernaUserClientsWithApiKeyAuth(
                    apiKey,
                    ApiScopes,
#if DEVENV
                    authority: "https://localhost:44379/",
#else
                    authority: EthernaUserClientsBuilder.DefaultSsoUrl,
#endif
                    httpClientName: HttpClientName,
                    configureHttpClient: c =>
                    {
                        c.Timeout = TimeSpan.FromMinutes(30);
                    });
            }
            ethernaClientsBuilder.AddEthernaGatewayClient(
#if DEVENV
                    gatewayBaseUrl: customGatewayUrl ?? "http://localhost:1633/"
#else
                    gatewayBaseUrl: customGatewayUrl ?? EthernaUserClientsBuilder.DefaultGatewayUrl
#endif
                    )
                .AddEthernaIndexClient(
#if DEVENV
                    indexBaseUrl: "https://localhost:44357/"
#else
                    indexBaseUrl: EthernaUserClientsBuilder.DefaultIndexUrl
#endif
                    );

            // Setup DI.
            //core
            services.AddCoreServices(
                videoImporterOptions =>
                {
                    videoImporterOptions.CustomWorkingDirectory = customWorkingDirectory;
                },
                cleanerOptions =>
                {
                    cleanerOptions.IsDryRun = isDryRun;
                },
                encoderOptions =>
                {
                    encoderOptions.IncludeAudioTrack = includeAudioTrack;
                },
                ffMpegOptions =>
                {
                    ffMpegOptions.BitrateReduction = bitrateReduction;
                    ffMpegOptions.CustomFFmpegFolderPath = customFFMpegFolderPath;
                    ffMpegOptions.Preset = ffmpegPreset;
                },
                gatewayOptions =>
                {
                    gatewayOptions.IsDryRun = isDryRun;
                    gatewayOptions.UseBeeApi = useBeeNativeNode;
                },
                uploaderOptions =>
                {
                    uploaderOptions.AcceptPurchaseOfAllBatches = autoPurchaseBatches;
                    uploaderOptions.IsDryRun = isDryRun;

                    if (!string.IsNullOrEmpty(ttlPostageBatchStr))
                    {
                        if (int.TryParse(ttlPostageBatchStr, CultureInfo.InvariantCulture, out var ttlPostageStamp))
                            uploaderOptions.TtlPostageStamp = TimeSpan.FromDays(ttlPostageStamp);
                        else
                            throw new ArgumentException($"Invalid value for TTL Postage Stamp");
                    }
                });

            //source provider
            switch (sourceType)
            {
                case SourceType.JsonList:
                    //options
                    services.Configure<JsonListVideoProviderOptions>(options =>
                    {
                        options.JsonMetadataUri = new BasicUUri(sourceUrls.First(), UUriKind.Local | UUriKind.OnlineAbsolute);
                    });
                    services.AddSingleton<IValidateOptions<JsonListVideoProviderOptions>, JsonListVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IVideoProvider, JsonListVideoProvider>();
                    break;
                case SourceType.YouTube:
                    //options
                    services.Configure<YouTubeVideoProviderOptions>(options =>
                    {
                        options.ImportAutogeneratedSubtitles = importAutogeneratedSubtitles;
                        options.SourceUrls = sourceUrls;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeVideoProviderOptions>, YouTubeVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IYoutubeClient>(_ =>
                    {
                        List<Cookie> cookies = [];
                        if (ytCookies is not null)
                        {
                            cookies.AddRange(ytCookies.Split(';').Select(c =>
                            {
                                var cStr = c.Trim();
                                var eqIndex = cStr.IndexOf('=');
                                if (eqIndex < 0)
                                    throw new InvalidOperationException($"Invalid cookie {cStr}");
                                var cKey = cStr[..eqIndex];
                                var cValue = cStr[(eqIndex + 1)..];

                                return new Cookie(cKey, cValue)
                                {
                                    Domain = "youtube.com",
                                };
                            }));
                        }
                        return new YoutubeClient(cookies);
                    });
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
