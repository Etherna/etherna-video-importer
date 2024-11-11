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

using Etherna.Sdk.Users;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.FFmpeg;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Devcon.Options;
using Etherna.VideoImporter.Devcon.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter.Devcon
{
    internal static class Program
    {
        // Consts.
        private static readonly string[] ApiScopes = ["userApi.gateway", "userApi.index"];
        private static readonly string HelpText = $"""
            Tool to import videos on Etherna from Devcon archive.
            
                Program distributed under AGPLv3 license. Copyright since 2022 by Etherna SA.
                You can find source code at: https://github.com/Etherna/etherna-video-importer
            
            Usage:  evid VIDEO_FOLDER [OPTIONS]

            General Options:
              -k, --api-key           Api Key (optional)
              -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
              -i, --ignore-update     Ignore new version of EthernaVideoImporter
              -a, --auto-purchase     Accept automatically purchase of all batches
              --dry                   Run in dry mode. Any action on swarm gateway or index is performed read-only
              --yt-cookies            List of cookies to use with YouTube requests, divided by ';'
              --yt-cookies-file       File path with list of cookies to use with YouTube requests, divided by ';'
              --working-dir           Use custom working directory

            Video Management Options:
              -t, --ttl               TTL (days) Postage Stamp (default: {VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays} days)
              -o, --offer             Offer video downloads to everyone
              --no-pin                Don't pin videos (pinning by default)
              --force                 Force upload video if they already have been uploaded
              -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
              --remove-unrecognized   Remove indexed videos not generated with this tool
              -u, --unpin             Try to unpin contents removed from index
              --bitrate-reduction     Reduce bitrate from HLS standard. [None, Low, Normal, High, Extreme, Insane]. Default: {FFmpegServiceOptions.DefaultBitrateReduction}
              --ffmpeg-preset         Preset option with ffmpeg (https://trac.ffmpeg.org/wiki/Encode/H.264#Preset). Default: {FFmpegServiceOptions.DefaultFFmpegPreset}

            Gateway Options:
              --bee-node              Use bee node as gateway
              --gateway-url           Connect gateway with custom URL

            Run 'evid -h' or 'evid --help' to print help.
            """;
        private const string HttpClientName = "ethernaAuthnHttpClient";

        // Methods.
        static async Task Main(string[] args)
        {
            // Parse arguments.
            string jsonSourceFolderPath;

            string? apiKey = null;
            string? customFFMpegFolderPath = null;
            bool ignoreUpdate = false;
            bool autoPurchaseBatches = false;
            bool isDryRun = false;
            string? ytCookies = null;
            string? customWorkingDirectory = null;

            string? ttlPostageStampStr = null;
            bool offerVideos = false;
            bool pinVideos = true;
            bool forceVideoUpload = false;
            bool removeMissingVideosFromSource = false;
            bool removeUnrecognizedVideos = false;
            bool unpinRemovedVideos = false;
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
                }
            }

            //json source folder
            jsonSourceFolderPath = args[0];
            if (!Directory.Exists(jsonSourceFolderPath))
            {
                Console.WriteLine($"Not found Json directory path {jsonSourceFolderPath}");
                throw new ArgumentException("Not found Json directory path");
            }

            //options
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
                    
                    case "--dry":
                        Console.WriteLine("Dry Run");
                        isDryRun = true;
                        break;
                    
                    case "--yt-cookies":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("YT cookies are missing");
                        ytCookies = optArgs[++i];
                        break;
                    
                    case "--yt-cookies-file":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("YT cookies file path is missing");
                        var ytCookiesFilePath = optArgs[++i];
                        ytCookies = await File.ReadAllTextAsync(ytCookiesFilePath);
                        break;
                    
                    case "--working-dir":
                        if (optArgs.Length == i + 1)
                            throw new ArgumentException("Working directory is missing");
                        customWorkingDirectory = optArgs[++i];
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
                        throw new ArgumentException(optArgs[i] + " is not a valid option");
                }
            }

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

                    if (!string.IsNullOrEmpty(ttlPostageStampStr))
                    {
                        if (int.TryParse(ttlPostageStampStr, CultureInfo.InvariantCulture, out var ttlPostageStamp))
                            uploaderOptions.TtlPostageStamp = TimeSpan.FromDays(ttlPostageStamp);
                        else
                            throw new ArgumentException($"Invalid value for TTL Postage Stamp");
                    }
                });
            
            //source provider
            services.Configure<DevconVideoProviderOptions>(options =>
            {
                options.DevconSourceFolderPath = jsonSourceFolderPath;
            });
            services.AddSingleton<IValidateOptions<DevconVideoProviderOptions>, DevconVideoProviderOptionsValidation>();
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
            services.AddTransient<IVideoProvider, DevconVideoProvider>();
            
            //result reporter
            services.Configure<DevconResultReporterOptions>(options =>
            {
                options.IsDryRun = isDryRun;
                options.ResultFolderPath = jsonSourceFolderPath;
            });
            services.AddSingleton<IValidateOptions<DevconResultReporterOptions>, DevconResultReporterOptionsValidation>();
            services.AddTransient<IResultReporterService, DevconResultReporterService>();

            var serviceProvider = services.BuildServiceProvider();

            // Start importer.
            var importer = serviceProvider.GetRequiredService<IEthernaVideoImporter>();
            await importer.RunAsync(
                removeUnrecognizedVideos,
                removeMissingVideosFromSource,
                forceVideoUpload,
                offerVideos,
                pinVideos,
                unpinRemovedVideos,
                ignoreUpdate);
        }
    }
}
