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
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            "  --Skip1440\tSkip upload resolution 1440p\n" +
            "  --Skip1080\tSkip upload resolution 1080p\n" +
            "  --Skip720\tSkip upload resolution 720p\n" +
            "  --Skip480\tSkip upload resolution 480p\n" +
            "  --Skip360\tSkip upload resolution 360p\n" +
            "\n" +
            "Run 'EthernaVideoImporter.Devcon -h' to print help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? mdSourceFolderPath = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteVideosMissingFromSource = false;
            bool deleteExogenousVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            bool unpinRemovedVideos = false;
            bool forceUploadVideo = false;
            bool acceptPurchaseOfAllBatches = false;
            bool ignoreNewVersionOfImporter = false;
            bool skip1440 = false;
            bool skip1080 = false;
            bool skip720 = false;
            bool skip480 = false;
            bool skip360 = false;

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
                    case "-o": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-m": deleteVideosMissingFromSource = true; break;
                    case "-e": deleteExogenousVideos = true; break;
                    case "-u": unpinRemovedVideos = true; break;
                    case "-f": forceUploadVideo = true; break;
                    case "-y": acceptPurchaseOfAllBatches = true; break;
                    case "-i": ignoreNewVersionOfImporter = true; break;
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

            var ethernaUserClients = new EthernaUserClients(
                new Uri(CommonConsts.EthernaCreditUrl),
                new Uri(CommonConsts.EthernaGatewayUrl),
                new Uri(CommonConsts.EthernaIndexUrl),
                new Uri(CommonConsts.EthernaSsoUrl),
                () => httpClient);
            using var beeNodeClient = new BeeNodeClient(
                CommonConsts.EthernaGatewayUrl,
                CommonConsts.BeeNodeGatewayPort,
                null,
                CommonConsts.BeeNodeGatewayVersion,
                CommonConsts.BeeNodeDebugVersion,
                httpClient);
            var videoUploaderService = new VideoUploaderService(
                beeNodeClient,
                ethernaUserClients.GatewayClient,
                ethernaUserClients.IndexClient,
                userEthAddr,
                TimeSpan.FromDays(ttlPostageStamp),
                acceptPurchaseOfAllBatches);
            var ffMpegMuxingService = new EncoderService(ffMpegBinaryPath, GetSupportedHeightResolutions(skip1440, skip1080, skip720, skip480, skip360));

            // Check for new version
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync(httpClient);
            if (newVersionAvaiable && !ignoreNewVersionOfImporter)
                return;

            // Call runner.
            var importer = new EthernaVideoImporter(
                new CleanerVideoService(
                    ethernaUserClients.GatewayClient,
                    ethernaUserClients.IndexClient),
                ethernaUserClients.GatewayClient,
                ethernaUserClients.IndexClient,
                new DevconLinkReporterService(mdSourceFolderPath),
                new MdVideoProvider(
                    mdSourceFolderPath,
                    ffMpegMuxingService,
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

        // Helpers.
        private static IEnumerable<int> GetSupportedHeightResolutions(
            bool skip1440,
            bool skip1080,
            bool skip720,
            bool skip480,
            bool skip360)
        {
            var supportedHeightResolutions = new List<int>();
            if (!skip1440)
                supportedHeightResolutions.Add(1440);
            if (!skip1080)
                supportedHeightResolutions.Add(1080);
            if (!skip720)
                supportedHeightResolutions.Add(720);
            if (!skip480)
                supportedHeightResolutions.Add(480);
            if (!skip360)
                supportedHeightResolutions.Add(360);

            return supportedHeightResolutions;
        }
    }
}
