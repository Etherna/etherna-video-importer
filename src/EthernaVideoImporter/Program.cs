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
using Etherna.VideoImporter.Services;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private const int DefaultTTLPostageStamp = 365;
        private const string DefaultFFmpegFolder = @".\FFmpeg\";
        private static readonly string HelpText =
            "Etherna Video Importer help:\n\n" +
            "-yc\tYoutube channel url\n" +
            "-yv\tYoutube single video url\n" +
            "-f\tFree videos offered by creator\n" +
            "-p\tPin video\n" +
            "-d\tRemove old videos that are no longer in source channel\n" +
            "-c\tRemove present videos not uploaded with this tool from channel\n" +
            $"-ff\tPath FFmpeg (default dir: {DefaultFFmpegFolder})\n" +
            $"-t\tTTL (days) Postage Stamp (default value: {DefaultTTLPostageStamp} days)\n" +
            "\n" +
            "-h\tPrint help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? youtubeChannelUrl = null;
            string? youtubeVideoUrl = null;
            string ffMpegFolderPath = DefaultFFmpegFolder;
            string? ttlPostageStampStr = null;
            int ttlPostageStamp = DefaultTTLPostageStamp;
            bool offerVideos = false;
            bool pinVideos = false;
            bool deleteSourceRemovedVideos = false;
            bool deleteVideosFromOtherSources = false;
            bool includeAudioTrack = false; //temporary disabled until https://etherna.atlassian.net/browse/EVI-21
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-yc": youtubeChannelUrl = args[++i]; break;
                    case "-yv": youtubeVideoUrl = args[++i]; break;
                    case "-ff": ffMpegFolderPath = args[++i]; break;
                    case "-f": offerVideos = true; break;
                    case "-p": pinVideos = true; break;
                    case "-d": deleteSourceRemovedVideos = true; break;
                    case "-c": deleteVideosFromOtherSources = true; break;
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

            //sources
            var sourceParam = 0;
            sourceParam += string.IsNullOrWhiteSpace(youtubeChannelUrl) ? 0 : 1;
            sourceParam += string.IsNullOrWhiteSpace(youtubeVideoUrl) ? 0 : 1;
            switch (sourceParam)
            {
                case 0:
                    Console.WriteLine("Missing required source param (one of: -ch or -yt)");
                    return;
                case > 1:
                    Console.WriteLine("Select only one source param (one of: -ch or -yt)");
                    return;
            }
            var sourceUri = youtubeChannelUrl ?? youtubeVideoUrl!;

            // Sign with SSO and create auth client.
            var authResult = await SignServices.SigInSSO().ConfigureAwait(false);
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
                new CleanerVideoService(ethernaUserClients.IndexClient),
                ethernaUserClients.IndexClient,
                new LinkReporterService(),
                new YouTubeChannelVideoProvider(
                    sourceUri,
                    ffMpegBinaryPath,
                    includeAudioTrack),
                videoUploaderService);

            await importer.RunAsync(
                userEthAddr,
                offerVideos,
                pinVideos,
                deleteSourceRemovedVideos,
                deleteVideosFromOtherSources).ConfigureAwait(false);
        }
    }
}
