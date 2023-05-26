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
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.SSO;
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Options;
using Etherna.VideoImporter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Etherna.VideoImporter
{
    internal static class Program
    {
        // Consts.
        private static readonly string HelpText =
            "\n" +
            "Usage:\tEthernaVideoImporter SOURCE_TYPE SOURCE_URI [OPTIONS]\n" +
            "\n" +
            "Source types:\n" +
            "  ytchannel\tYouTube channel\n" +
            "  ytvideo\tYouTube video\n" +
            "  local\tLocal videos\n" +
            "\n" +
            "See Readme to get info on how to use the local videos source." +
            "\n" +
            "Options:\n" +
            $"  -ff\tPath FFmpeg (default dir: {CommonConsts.DefaultFFmpegFolder})\n" +
            $"  -hw\tUse hardware acceleration on FFmpeg (default: {nameof(FFMpegHwAccelerations.None).ToLowerInvariant()}). Valid values: [{Enum.GetNames<FFMpegHwAccelerations>().Aggregate((r, i) => $"{r}, {i}").ToLowerInvariant()}]\n" +
            $"  -t\tTTL (days) Postage Stamp (default value: {VideoUploaderServiceOptions.DefaultTtlPostageStamp.TotalDays} days)\n" +
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
            bool skip1440 = false;
            bool skip1080 = false;
            bool skip720 = false;
            bool skip480 = false;
            bool skip360 = false;
            bool useBeeNativeNode = false;
            var ffMpegHwAcceleration = FFMpegHwAccelerations.None;

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

                case "local":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Local videos JSON metadata path is missing");
                        throw new ArgumentException("Invalid argument");
                    }
                    sourceType = SourceType.LocalVideos;
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
                        customFFMpegFolderPath = args[++i];
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
                    case "-f": forceVideoUpload = true; break;
                    case "-y": acceptPurchaseOfAllBatches = true; break;
                    case "-i": ignoreNewVersionsOfImporter = true; break;
                    case "-skip1440": skip1440 = true; break;
                    case "-skip1080": skip1080 = true; break;
                    case "-skip720": skip720 = true; break;
                    case "-skip480": skip480 = true; break;
                    case "-skip360": skip360 = true; break;
                    case "-hw": ffMpegHwAcceleration = Enum.Parse<FFMpegHwAccelerations>(args[++i], true); break;
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
            var userEthAddr = authResult.User.Claims.Where(i => i.Type == EthernaClaimTypes.EtherAddress).First().Value;
            Console.WriteLine($"User {authResult.User.Claims.Where(i => i.Type == EthernaClaimTypes.Username).First().Value} autenticated");

            // Check for new versions.
            var newVersionAvaiable = await EthernaVersionControl.CheckNewVersionAsync();
            if (newVersionAvaiable && !ignoreNewVersionsOfImporter)
                return;

            // Setup DI.
            var services = new ServiceCollection();

            services.AddCoreServices(
                encoderOptions =>
                {
                    if (customFFMpegFolderPath is not null)
                        encoderOptions.FFMpegFolderPath = customFFMpegFolderPath;
                    encoderOptions.FFMpegHwAcceleration = ffMpegHwAcceleration;
                    encoderOptions.IncludeAudioTrack = includeAudioTrack;
                    encoderOptions.Skip1440 = skip1440;
                    encoderOptions.Skip1080 = skip1080;
                    encoderOptions.Skip720 = skip720;
                    encoderOptions.Skip480 = skip480;
                    encoderOptions.Skip360 = skip360;
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

                    uploaderOptions.UserEthAddr = userEthAddr;
                },
                useBeeNativeNode,
                authResult.RefreshTokenHandler);

            switch (sourceType.Value)
            {
                case SourceType.LocalVideos:
                    //options
                    services.Configure<LocalVideoProviderOptions>(options =>
                    {
                        if (customFFMpegFolderPath is not null)
                            options.FFProbeFolderPath = customFFMpegFolderPath;
                        options.JsonMetadataFilePath = sourceUri;
                    });
                    services.AddSingleton<IValidateOptions<LocalVideoProviderOptions>, LocalVideoProviderOptionsValidation>();

                    //services
                    services.AddTransient<IVideoProvider, LocalVideoProvider>();
                    break;
                case SourceType.YouTubeChannel:
                    //options
                    services.Configure<YouTubeChannelVideoProviderOptions>(options =>
                    {
                        options.ChannelUrl = sourceUri;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeChannelVideoProviderOptions>, YouTubeChannelVideoProviderOptionsValidation>();

                    //services
                    AddYoutubeDownloader(services, ffMpegHwAcceleration);
                    services.AddTransient<IVideoProvider, YouTubeChannelVideoProvider>();
                    break;
                case SourceType.YouTubeVideo:
                    //options
                    services.Configure<YouTubeSingleVideoProviderOptions>(options =>
                    {
                        options.VideoUrl = sourceUri;
                    });
                    services.AddSingleton<IValidateOptions<YouTubeSingleVideoProviderOptions>, YouTubeSingleVideoProviderOptionsValidation>();

                    //services
                    AddYoutubeDownloader(services, ffMpegHwAcceleration);
                    services.AddTransient<IVideoProvider, YouTubeSingleVideoProvider>();
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Start importer.
            var importer = serviceProvider.GetRequiredService<IEthernaVideoImporter>();
            await importer.RunAsync(
                deleteExogenousVideos,
                deleteVideosMissingFromSource,
                forceVideoUpload,
                offerVideos,
                pinVideos,
                userEthAddr,
                unpinRemovedVideos);
        }

        // Helpers.
        private static void AddYoutubeDownloader(
            ServiceCollection services,
            FFMpegHwAccelerations hwAcceleration)
        {
            services.AddTransient<IYoutubeClient>(_ =>
                hwAcceleration switch
                {
                    FFMpegHwAccelerations.None => new YoutubeClient(),
                    FFMpegHwAccelerations.Cuda => new YoutubeClient("cuda"),
                    _ => throw new InvalidOperationException()
                });
            services.AddTransient<IYoutubeDownloader, YoutubeDownloader>();
        }
    }
}
