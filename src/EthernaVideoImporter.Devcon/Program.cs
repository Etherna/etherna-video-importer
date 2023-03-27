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
            $"  --BeeGatewayUrl\tUrl of gateway Bee node (default value: {CommonConsts.EthernaGatewayUrl})\n" +
            $"  --BeeGatewayApiPort\tPort used by API Gateway (default value: {CommonConsts.BeeNodeGatewayPort})\n" +
            $"  --BeeGatewayDebugPort\tPort used by Debug Gateway (default value: {CommonConsts.BeeNodeDebugGatewayPort})\n" +
            "\n" +
            "Run 'EthernaVideoImporter.Devcon -h' to print help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? mdSourceFolderPath = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            string? gatewayApiPortStr = null;
            string? gatewayDebugPortStr = null;
            string gatewayUrl = CommonConsts.EthernaGatewayUrl;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            int gatewayApiPort = CommonConsts.BeeNodeGatewayPort;
            int gatewayDebugPort = CommonConsts.BeeNodeDebugGatewayPort;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteVideosMissingFromSource = false;
            bool deleteExogenousVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            bool unpinRemovedVideos = false;
            bool forceUploadVideo = false;
            bool acceptPurchaseOfAllBatches = false;
            bool ignoreNewVersionOfImporter = false;
            bool nativeBeeGateway = false;

            // Parse input.
            if (args.Length == 0)
            {
                Console.WriteLine(HelpText);
                return;
            }

            switch (args[0].ToLower(CultureInfo.InvariantCulture))
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
                    case "--beegateway":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Gateway value is missing");
                        gatewayUrl = args[++i];
                        nativeBeeGateway = true;
                        if (!gatewayUrl.EndsWith("/", StringComparison.InvariantCulture))
                            gatewayUrl += "/";
                        break;
                    case "--beegatewayapiport":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Gateway API port missing");
                        gatewayApiPortStr = args[++i];
                        break;
                    case "--beegatewaydebugport":
                        if (args.Length == i + 1)
                            throw new InvalidOperationException("Gateway Debug port missing");
                        gatewayDebugPortStr = args[++i];
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
                nativeBeeGateway)
            {
                Console.WriteLine($"Only Etherna Gateway support offer video downloads to everyone");
                return;
            }

            //gateway api port
            if (!string.IsNullOrEmpty(gatewayApiPortStr) &&
                !int.TryParse(gatewayApiPortStr, CultureInfo.InvariantCulture, out gatewayApiPort))
            {
                Console.WriteLine($"Invalid value for Gateway API port");
                return;
            }

            //gateway debug port
            if (!string.IsNullOrEmpty(gatewayDebugPortStr) &&
                !int.TryParse(gatewayDebugPortStr, CultureInfo.InvariantCulture, out gatewayDebugPort))
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

            //bee
            using var beeNodeClient = new BeeNodeClient(
                gatewayUrl,
                gatewayApiPort,
                gatewayDebugPort,
                CommonConsts.BeeNodeGatewayVersion,
                CommonConsts.BeeNodeDebugVersion,
                httpClient);

            //index
            var ethernaIndexUserClients = new EthernaUserClients(
                new Uri(CommonConsts.EthernaCreditUrl),
                new Uri(CommonConsts.EthernaGatewayUrl),
                new Uri(CommonConsts.EthernaIndexUrl),
                new Uri(CommonConsts.EthernaSsoUrl),
                () => httpClient);

            //gateway
            IGatewayService gatewayService = nativeBeeGateway ?
                            new BeeGatewayService(beeNodeClient.GatewayClient!) :
                            new EthernaGatewayService(ethernaIndexUserClients.GatewayClient);

            //video uploader service
            var videoUploaderService = new VideoUploaderService(
                beeNodeClient,
                gatewayService,
                ethernaIndexUserClients.IndexClient,
                userEthAddr,
                TimeSpan.FromDays(ttlPostageStamp),
                acceptPurchaseOfAllBatches);

            // Check for new version
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync(httpClient);
            if (newVersionAvaiable && !ignoreNewVersionOfImporter)
                return;

            // Call runner.
            var importer = new EthernaVideoImporter(
                new CleanerVideoService(
                    gatewayService,
                    ethernaIndexUserClients.IndexClient),
                gatewayService,
                ethernaIndexUserClients.IndexClient,
                new DevconLinkReporterService(mdSourceFolderPath),
                new MdVideoProvider(
                    mdSourceFolderPath,
                    ffMpegBinaryPath,
                    includeAudioTrack),
                videoUploaderService);

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
