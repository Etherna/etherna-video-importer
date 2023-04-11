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

using Etherna.Authentication;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.SSO;
using Etherna.VideoImporter.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private const int DefaultTTLPostageStamp = 365;
        private const string DefaultFFmpegFolder = @".\FFmpeg\";
        private static readonly string HelpText =
            "\n" +
            "Usage:\tEthernaVideoImporter SOURCE_TYPE SOURCE_URI [OPTIONS]\n" +
            "\n" +
            "Source types:\n" +
            "  ytchannel\tYouTube channel\n" +
            "  ytvideo\tYouTube video\n" +
            "\n" +
            "Options:\n" +
            $"  -ff\tPath FFmpeg (default dir: {DefaultFFmpegFolder})\n" +
            $"  -t\tTTL (days) Postage Stamp (default value: {DefaultTTLPostageStamp} days)\n" +
            "  -o\tOffer video downloads to everyone\n" +
            "  -p\tPin videos\n" +
            "  -m\tRemove indexed videos generated with this tool but missing from source\n" +
            "  -e\tRemove indexed videos not generated with this tool\n" +
            "  -u\tTry to unpin contents removed from index\n" +
            "  -f\tForce upload video if they already has been uploaded\n" +
            "  -y\tAccept automatically purchase of all batches\n" +
            "  -i\tIgnore new versions of EthernaVideoImporter\n" +
            "  --beenode\tUse bee native node\n" +
            $"  --beenodeurl\tUrl of Bee node (default value: {CommonConsts.BeeNodeUrl})\n" +
            $"  --beenodeapiport\tPort used by API (default value: {CommonConsts.BeeApiPort})\n" +
            $"  --beenodedebugport\tPort used by Debug (default value: {CommonConsts.BeeDebugPort})\n" +
            "  --skip1440\tSkip upload resolution 1440p\n" +
            "  --skip1080\tSkip upload resolution 1080p\n" +
            "  --skip720\tSkip upload resolution 720p\n" +
            "  --skip480\tSkip upload resolution 480p\n" +
            "  --skip360\tSkip upload resolution 360p\n" +
            "\n" +
            "Run 'EthernaVideoImporter -h' to print help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            SourceType? sourceType = null;
            string? sourceUri = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            string? beeNodeApiPortStr = null;
            string? beeNodeDebugPortStr = null;
            string beeNodeUrl = CommonConsts.BeeNodeUrl;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            int beeNodeApiPort = CommonConsts.BeeApiPort;
            int beeNodeDebugPort = CommonConsts.BeeDebugPort;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteVideosMissingFromSource = false;
            bool deleteExogenousVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            bool unpinRemovedVideos = false;
            bool forceUploadVideo = false;
            bool acceptPurchaseOfAllBatches = false;
            bool ignoreNewVersionsOfImporter = false;
            bool skip1440 = false;
            bool skip1080 = false;
            bool skip720 = false;
            bool skip480 = false;
            bool skip360 = false;
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

                case "ytchannel":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("YouTube Channel url is missing");
                        throw new ArgumentException("Invalid argument");
                    }
                    sourceType = SourceType.YouTubeChannel;
                    sourceUri = args[1];
                    break;

                case "ytvideo":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("YouTube Video url is missing");
                        throw new ArgumentException("Invalid argument");
                    }
                    sourceType = SourceType.YouTubeVideo;
                    sourceUri = args[1];
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
                    case "-ff":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("ffMpeg folder is missing");
                        ffMpegFolderPath = args[++i];
                        break;
                    case "-t":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("TTL value is missing");
                        ttlPostageStampStr = args[++i];
                        break;
                    case "--beenode":
                        useBeeNativeNode = true;
                        break;
                    case "--beenodeurl":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Bee node value is missing");
                        beeNodeUrl = args[++i];
                        useBeeNativeNode = true;
                        if (!beeNodeUrl.EndsWith("/", StringComparison.InvariantCulture))
                            beeNodeUrl += "/";
                        break;
                    case "--beenodeapiport":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Bee node API port missing");
                        beeNodeApiPortStr = args[++i];
                        useBeeNativeNode = true;
                        break;
                    case "--beenodedebugport":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Bee node Debug port missing");
                        beeNodeDebugPortStr = args[++i];
                        useBeeNativeNode = true;
                        break;
                    case "-o": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-m": deleteVideosMissingFromSource = true; break;
                    case "-e": deleteExogenousVideos = true; break;
                    case "-u": unpinRemovedVideos = true; break;
                    case "-f": forceUploadVideo = true; break;
                    case "-y": acceptPurchaseOfAllBatches = true; break;
                    case "-i": ignoreNewVersionsOfImporter = true; break;
                    case "-skip1440": skip1440 = true; break;
                    case "-skip1080": skip1080 = true; break;
                    case "-skip720": skip720 = true; break;
                    case "-skip480": skip480 = true; break;
                    case "-skip360": skip360 = true; break;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Input validation.
            //FFmpeg
            var ffMpegBinaryPath = Path.Combine(ffMpegFolderPath, CommonConsts.FFMpegBinaryName);
            if (!File.Exists(ffMpegBinaryPath))
            {
                Console.WriteLine($"FFmpeg not found at ({ffMpegBinaryPath})");
                return;
            }

            //ttl postage batch
            if (!string.IsNullOrEmpty(ttlPostageStampStr) &&
                !int.TryParse(ttlPostageStampStr, CultureInfo.InvariantCulture, out ttlPostageStamp))
            {
                Console.WriteLine($"Invalid value for TTL Postage Stamp");
                return;
            }

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

            //deny delete video old sources when is single
            if (sourceType == SourceType.YouTubeVideo && deleteVideosMissingFromSource)
            {
                Console.WriteLine($"Cannot delete video removed from source when the source is a single video");
                return;
            }

            // Sign with SSO and create auth client.
            var authResult = await SignServices.SigInSSO();
            if (authResult.IsError)
            {
                Console.WriteLine($"Error during authentication");
                Console.WriteLine(authResult.Error);
                return;
            }
            var userEthAddr = authResult.User.Claims.Where(i => i.Type == EthernaClaimTypes.EtherAddress).FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(userEthAddr))
            {
                Console.WriteLine($"Missing ether address");
                return;
            }
            Console.WriteLine($"User {authResult.User.Claims.Where(i => i.Type == EthernaClaimTypes.Username).FirstOrDefault()?.Value} autenticated");

            // Migration service.
            var migrationService = new MigrationService();

            // Check for new version
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync();
            if (newVersionAvaiable && !ignoreNewVersionsOfImporter)
                return;

            // Setup DI.
            var services = new ServiceCollection();

            //configure
            services.AddImportSettings(
                acceptPurchaseOfAllBatches,
                deleteExogenousVideos,
                deleteVideosMissingFromSource,
                ffMpegBinaryPath,
                ffMpegFolderPath,
                forceUploadVideo,
                ignoreNewVersionsOfImporter,
                includeAudioTrack,
                offerVideos,
                pinVideos,
                sourceUri,
                skip1440,
                skip1080,
                skip720,
                skip480,
                skip360,
                Path.Combine(Path.GetTempPath(), CommonConsts.ImporterIdentifier),
                ttlPostageStamp,
                unpinRemovedVideos,
                userEthAddr)
            .AddLinkReporterService()
            .AddVideoProvider(sourceType.Value)
            .AddCommonServices(useBeeNativeNode)
            .ConfigureHttpClient(authResult.RefreshTokenHandler);

            var serviceProvider = services.BuildServiceProvider();

            //do the actual work here
            var importer = serviceProvider.GetService<EthernaVideoImporter>() 
                ?? throw new InvalidOperationException("Cannot resolve EthernaVideoImporter service");
            await importer.RunAsync();
        }

        // Helpers.
        private static IServiceCollection AddLinkReporterService(this IServiceCollection services)
        {
            return services.AddTransient<ILinkReporterService, LinkReporterService>();
        }

        private static IServiceCollection AddVideoProvider(this IServiceCollection services, SourceType sourceType)
        {
            if (sourceType == SourceType.YouTubeChannel)
                return services.AddTransient<IVideoProvider, YouTubeChannelVideoProvider>();
            else if (sourceType == SourceType.YouTubeVideo)
                return services.AddTransient<IVideoProvider, YouTubeSingleVideoProvider>();
            else
                throw new InvalidOperationException();
        }
    }
}
