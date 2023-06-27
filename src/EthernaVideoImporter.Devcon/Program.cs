//   Copyright 2022-present Etherna Sagl
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

using Etherna.ServicesClient.Users.Native;
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
        private static readonly string HelpText =
            "\n" +
            "Usage:\tEthernaVideoImporter.Devcon md MD_FOLDER [OPTIONS]\n" +
            "\n" +
            "Options:\n" +
            $"  -ff <path>\tPath FFmpeg (default dir: {CommonConsts.DefaultFFmpegFolder})\n" +
            $"  -t <days>\tTTL (days) Postage Stamp (default value: {VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays} days)\n" +
            "  -ak <key>\tApi Key (optional)" +
            "  -o\tOffer video downloads to everyone\n" +
            "  -p\tPin videos\n" +
            "  -m\tRemove indexed videos generated with this tool but missing from source\n" +
            "  -e\tRemove indexed videos not generated with this tool\n" +
            "  -u\tTry to unpin contents removed from index\n" +
            "  -f\tForce upload video if they already has been uploaded\n" +
            "  -y\tAccept automatically purchase of all batches\n" +
            "  -i\tIgnore new versions of EthernaVideoImporter.Devcon\n" +
            "  --beenode\tUse bee native node\n" +
            $"  --beenodeurl <url>\tUrl of Bee node (default value: {CommonConsts.BeeNodeUrl})\n" +
            $"  --beenodeapiport <apiPort>\tPort used by API (default value: {CommonConsts.BeeApiPort})\n" +
            $"  --beenodedebugport <debugPort>\tPort used by Debug (default value: {CommonConsts.BeeDebugPort})\n" +
            "\n" +
            "Run 'EthernaVideoImporter.Devcon -h' to print help\n";
        private const string HttpClientName = "ethernaAuthnHttpClient";

        // Methods.
        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? mdSourceFolderPath = null;
            string? apiKey = null;
            string? customFFMpegFolderPath = null;
            string? ttlPostageStampStr = null;
            string? beeNodeApiPortStr = null;
            string? beeNodeDebugPortStr = null;
            string beeNodeUrl = CommonConsts.BeeNodeUrl;
            int beeNodeApiPort = CommonConsts.BeeApiPort;
            int beeNodeDebugPort = CommonConsts.BeeDebugPort;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteVideosMissingFromSource = false;
            bool deleteExogenousVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            bool unpinRemovedVideos = false;
            bool forceVideoUpload = false;
            bool acceptPurchaseOfAllBatches = false;
            bool ignoreNewVersionsOfImporter = false;
            bool useBeeNativeNode = false;

            // Parse input.
            if (args.Length == 0)
            {
                Console.WriteLine(HelpText);
                return;
            }

            switch (args[0])
            {
                case "-h":
                    Console.WriteLine(HelpText);
                    return;

                case "md":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("MD file folder is missing");
                        throw new ArgumentException("Invalid argument");
                    }
                    if (string.IsNullOrWhiteSpace(args[1]) || !Directory.Exists(args[1]))
                    {
                        Console.WriteLine($"Not found MD directory path {args[1]}");
                        throw new ArgumentException("Not found MD directory path");
                    }
                    mdSourceFolderPath = args[1];
                    break;

                default:
                    Console.WriteLine($"Invalid argument");
                    Console.WriteLine(HelpText);
                    throw new ArgumentException("Invalid argument");
            }

            // Get params.
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-ak":
                        if (args.Length == i + 1)
                            throw new ArgumentException("Api Key is missing");
                        apiKey = args[++i];
                        break;
                    case "-ff":
                        if (args.Length == i + 1)
                            throw new ArgumentException("FFmpeg folder is missing");
                        customFFMpegFolderPath = args[++i];
                        break;
                    case "-t":
                        if (args.Length == i + 1)
                            throw new ArgumentException("TTL value is missing");
                        ttlPostageStampStr = args[++i];
                        break;
                    case "--beenode":
                        useBeeNativeNode = true;
                        break;
                    case "--beenodeurl":
                        if (args.Length == i + 1)
                            throw new ArgumentException("Bee node value is missing");
                        beeNodeUrl = args[++i];
                        useBeeNativeNode = true;
                        if (!beeNodeUrl.EndsWith("/", StringComparison.InvariantCulture))
                            beeNodeUrl += "/";
                        break;
                    case "--beenodeapiport":
                        if (args.Length == i + 1)
                            throw new ArgumentException("Bee node API port missing");
                        beeNodeApiPortStr = args[++i];
                        useBeeNativeNode = true;
                        break;
                    case "--beenodedebugport":
                        if (args.Length == i + 1)
                            throw new ArgumentException("Bee node Debug port missing");
                        beeNodeDebugPortStr = args[++i];
                        useBeeNativeNode = true;
                        break;
                    case "-o": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-m": deleteVideosMissingFromSource = true; break;
                    case "-e": deleteExogenousVideos = true; break;
                    case "-u": unpinRemovedVideos = true; break;
                    case "-f": forceVideoUpload = true; break;
                    case "-y": acceptPurchaseOfAllBatches = true; break;
                    case "-i": ignoreNewVersionsOfImporter = true; break;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Input validation.
            //offer video
            if (offerVideos &&
                useBeeNativeNode)
            {
                Console.WriteLine($"Only Etherna Gateway supports offering video downloads to everyone");
                return;
            }

            //bee node api port
            if (!string.IsNullOrEmpty(beeNodeApiPortStr) &&
                !int.TryParse(beeNodeApiPortStr, CultureInfo.InvariantCulture, out beeNodeApiPort))
            {
                Console.WriteLine($"Invalid value for Gateway API port");
                return;
            }

            //bee node debug port
            if (!string.IsNullOrEmpty(beeNodeDebugPortStr) &&
                !int.TryParse(beeNodeDebugPortStr, CultureInfo.InvariantCulture, out beeNodeDebugPort))
            {
                Console.WriteLine($"Invalid value for Gateway Debug port");
                return;
            }

            // Check for new versions.
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync();
            if (newVersionAvaiable && !ignoreNewVersionsOfImporter)
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
                    if (customFFMpegFolderPath is not null)
                        encoderOptions.FFMpegFolderPath = customFFMpegFolderPath;
                    encoderOptions.IncludeAudioTrack = includeAudioTrack;
                },
                uploaderOptions =>
                {
                    uploaderOptions.AcceptPurchaseOfAllBatches = acceptPurchaseOfAllBatches;

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
                deleteExogenousVideos,
                deleteVideosMissingFromSource,
                forceVideoUpload,
                offerVideos,
                pinVideos,
                unpinRemovedVideos);
        }
    }
}
