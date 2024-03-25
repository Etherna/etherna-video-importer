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
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Devcon.Options;
using Etherna.VideoImporter.Devcon.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter.Devcon
{
    internal static class Program
    {
        // Consts.
        private static readonly string[] ApiScopes = ["userApi.gateway", "userApi.index"];
        private static readonly string HelpText = $$"""
            Usage:  evid MD_FOLDER [OPTIONS]

            General Options:
              -k, --api-key           Api Key (optional)
              -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
              -i, --ignore-update     Ignore new version of EthernaVideoImporter
              -a, --auto-purchase     Accept automatically purchase of all batches
              -d, --disable-index     Disable listing video on index

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

            Run 'evid -h' or 'evid --help' to print help.
            """;
        private const string HttpClientName = "ethernaAuthnHttpClient";

        // Methods.
        static async Task Main(string[] args)
        {
            // Parse arguments.
            string mdSourceFolderPath;

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
            bool indexManifest = true;

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
                }
            }

            //md source folder
            mdSourceFolderPath = args[0];
            if (!Directory.Exists(mdSourceFolderPath))
            {
                Console.WriteLine($"Not found MD directory path {mdSourceFolderPath}");
                throw new ArgumentException("Not found MD directory path");
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

                    case "-d":
                    case "--disable-index":
                        indexManifest = false;
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
                        if (!beeNodeUrl.EndsWith('/'))
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
            //configure options
            services.Configure<MdVideoProviderOptions>(options =>
            {
                options.MdSourceFolderPath = mdSourceFolderPath;
            });
            services.AddSingleton<IValidateOptions<MdVideoProviderOptions>, MdVideoProviderOptionsValidation>();

            //add services
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
                    uploaderOptions.IndexManifest = indexManifest;

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
            services.AddTransient<IYoutubeClient, YoutubeClient>();
            services.AddTransient<IYoutubeDownloader, YoutubeDownloader>();
            services.AddTransient<IVideoProvider, MdVideoProvider>();

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
                indexManifest);
        }
    }
}
