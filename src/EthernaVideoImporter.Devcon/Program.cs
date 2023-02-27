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
            "Etherna Video Importer for Devcon Archive help:\n\n" +
            "md <folderMD>\tSource folder path with *.md files to import\n" +
            "-f\tFree videos offered by creator\n" +
            "-p\tPin video\n" +
            "-d\tRemove indexed videos deleted from source\n" +
            "-c\tRemove indexed videos not generated with this tool\n" +
            "-u\tTry to unpin videos removed from index\n" +
            $"-ff\tPath FFmpeg (default dir: {DefaultFFmpegFolder})\n" +
            $"-t\tTTL (days) Postage Stamp (default value: {DefaultTTLPostageStamp} days)\n" +
            "\n" +
            "-h\tPrint help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? mdSourceFolderPath = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteVideosRemovedFromSource = false;
            bool deleteExogenousVideos = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            bool unpinRemovedVideos = false;

            // Get MD path.
            if (args.Length < 2 ||
                args[0] != "md")
            {
                Console.WriteLine($"Missing MD directory path\n{HelpText}");
                throw new ArgumentException("Missing mandatory data");
            }
            if (string.IsNullOrWhiteSpace(args[1]) ||
                !Directory.Exists(args[1]))
            {
                Console.WriteLine($"Not found MD directory path\n{HelpText}");
                throw new ArgumentException("Not found MD directory path");
            }
            mdSourceFolderPath = args[1];

            // Get params.
            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-ff": ffMpegFolderPath = args[++i]; break;
                    case "-f": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-d": deleteVideosRemovedFromSource = true; break;
                    case "-c": deleteExogenousVideos = true; break;
                    case "-u": unpinRemovedVideos = true; break;
                    case "-t": ttlPostageStampStr = args[++i]; break;
                    case "-h": Console.Write(HelpText); return;
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
                int.TryParse(ttlPostageStampStr, CultureInfo.InvariantCulture, out ttlPostageStamp))
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
            var userEthAddr = authResult.User.Claims.Where(i => i.Type == "ether_address").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(userEthAddr))
            {
                Console.WriteLine($"Missing ether address");
                return;
            }

            // Inizialize services.
            using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };
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
                TimeSpan.FromDays(ttlPostageStamp));

            // Call runner.
            var importer = new EthernaVideoImporter(
                new CleanerVideoService(
                    ethernaUserClients.GatewayClient,
                    ethernaUserClients.IndexClient),
                ethernaUserClients.GatewayClient,
                ethernaUserClients.IndexClient,
                new DevconLinkReporterService(),
                new MdVideoProvider(
                    mdSourceFolderPath,
                    ffMpegBinaryPath,
                    includeAudioTrack),
                videoUploaderService);

            await importer.RunAsync(
                userEthAddr,
                offerVideos,
                pinVideos,
                deleteVideosRemovedFromSource,
                deleteExogenousVideos,
                unpinRemovedVideos);
        }
    }
}
