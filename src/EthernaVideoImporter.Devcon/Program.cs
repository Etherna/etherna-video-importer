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
using Etherna.BeeNet;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.SSO;
using Etherna.VideoImporter.Devcon.Services;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Devcon
{
    internal static class Program
    {
        // Consts.
        private const int DefaultTTLPostageStamp = 365;
        private const string DefaultFFmpegFolder = @".\FFmpeg\";
        private static readonly string HelpText =
            "\n" +
            "Usage:\tEthernaVideoImporter.Devcon md MD_FOLDER [OPTIONS]\n" +
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
            "  -i\tIgnore new version of EthernaVideoImporter.Devcon\n" +
            "  --beenode\tUse bee native node\n" +
            $"  --beenodeurl\tUrl of Bee node (default value: {CommonConsts.BeeNodeUrl})\n" +
            $"  --beenodeapiport\tPort used by API (default value: {CommonConsts.BeeApiPort})\n" +
            $"  --beenodedebugport\tPort used by Debug (default value: {CommonConsts.BeeDebugPort})\n" +
            "\n" +
            "Run 'EthernaVideoImporter.Devcon -h' to print help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? mdSourceFolderPath = null;
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
            bool ignoreNewVersionOfImporter = false;
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
                    case "-i": ignoreNewVersionOfImporter = true; break;
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

            // Inizialize services.
            using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromMinutes(30) };

            //index
            var ethernaUserClients = new EthernaUserClients(
                new Uri(CommonConsts.EthernaCreditUrl),
                new Uri(CommonConsts.EthernaGatewayUrl),
                new Uri(CommonConsts.EthernaIndexUrl),
                new Uri(CommonConsts.EthernaSsoUrl),
                () => httpClient);

            //bee
            using var beeNodeClient = new BeeNodeClient(
                useBeeNativeNode ? beeNodeUrl : CommonConsts.EthernaGatewayUrl,
                useBeeNativeNode ? beeNodeApiPort : CommonConsts.EthernaGatewayPort,
                useBeeNativeNode ? beeNodeDebugPort : null,
                CommonConsts.BeeNodeGatewayVersion,
                CommonConsts.BeeNodeDebugVersion,
                httpClient);

            //gateway
            IGatewayService gatewayService = useBeeNativeNode ?
                new BeeGatewayService(beeNodeClient.GatewayClient!) :
                new EthernaGatewayService(
                    beeNodeClient.GatewayClient!,
                    ethernaUserClients.GatewayClient);

            //video uploader service
            var videoUploaderService = new VideoUploaderService(
                gatewayService,
                ethernaUserClients.IndexClient,
                userEthAddr,
                TimeSpan.FromDays(ttlPostageStamp),
                acceptPurchaseOfAllBatches);

            // Migration service.
            var migrationService = new MigrationService();

            // Check for new version
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync();
            if (newVersionAvaiable && !ignoreNewVersionOfImporter)
                return;

            // Call runner.
            var importer = new EthernaVideoImporter(
                new CleanerVideoService(
                    ethernaUserClients.IndexClient,
                    gatewayService),
                gatewayService,
                ethernaUserClients.IndexClient,
                new DevconLinkReporterService(mdSourceFolderPath),
                new MdVideoProvider(
                    mdSourceFolderPath,
                    ffMpegBinaryPath,
                    includeAudioTrack),
                videoUploaderService,
                migrationService);

            await importer.RunAsync(
                userEthAddr,
                offerVideos,
                pinVideos,
                deleteVideosMissingFromSource,
                deleteExogenousVideos,
                unpinRemovedVideos,
                forceUploadVideo);
        }
    }
}
