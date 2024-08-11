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

using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.FFmpeg;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed partial class FFmpegService : IFFmpegService
    {
        // Consts.
        [GeneratedRegex(@"\s*Duration: (?<d>\d{2}:\d{2}:\d{2}\.\d{2})")]
        private static partial Regex DurationRegex();
        private readonly Dictionary<int, int> HlsBitrateByArea;
        private const string HlsMasterPlaylistName = "master.m3u8";
        private const string HlsStreamPlaylistName = "playlist.m3u8";
        
        // Fields.
        private readonly List<Command> activedCommands = new();
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly FFmpegServiceOptions options;
        private string? ffMpegBinaryPath;
        private string? ffProbeBinaryPath;
        private readonly IParsingService parsingService;
        private readonly IGatewayService gatewayService;
        private readonly IIoService ioService;
        private readonly IUFileProvider uFileProvider;

        public FFmpegService(IParsingService parsingService,
            IGatewayService gatewayService,
            IIoService ioService,
            IOptions<FFmpegServiceOptions> options,
            IUFileProvider uFileProvider)
        {
            this.parsingService = parsingService;
            this.gatewayService = gatewayService;
            this.ioService = ioService;
            this.options = options.Value;
            this.uFileProvider = uFileProvider;
            
            // Init tables.
            //source: https://developer.apple.com/documentation/http-live-streaming/hls-authoring-specification-for-apple-devices#Video
            var hlsBitrateByHeight = new Dictionary<int, int>
            {
                [0] = 145,
                [234] = 145,
                [360] = 365,
                [432] = 915, //avg of suggested
                [540] = 2000,
                [720] = 3750, //avg of suggested
                [1080] = 6900 //avg of suggested
            };
            HlsBitrateByArea = hlsBitrateByHeight.ToDictionary(
                pair => pair.Key * pair.Key * 16 / 9, //apple example is with 16:9,
                pair => pair.Value);
        }

        // Methods.
        public async Task<VideoEncodingBase> DecodeVideoEncodingFromUUriAsync(
            BasicUUri mainFileUri,
            SwarmAddress? swarmAddress = null)
        {
            ArgumentNullException.ThrowIfNull(mainFileUri, nameof(mainFileUri));
            ArgumentNullException.ThrowIfNull(uFileProvider, nameof(uFileProvider));

            var mainFileAbsoluteUri = mainFileUri.ToAbsoluteUri();
            var mainFile = await FileBase.BuildFromUFileAsync(
                uFileProvider.BuildNewUFile(mainFileAbsoluteUri));
            var ffProbeResult = await GetVideoInfoAsync(mainFileAbsoluteUri.OriginalUri);
            
            // Get main file directory.
            var masterFileDirectory = mainFileUri.TryGetParentDirectoryAsAbsoluteUri();
            if (masterFileDirectory is null)
                throw new InvalidOperationException($"Can't get parent directory of {mainFileUri.OriginalUri}");

            if (swarmAddress is not null)
                mainFile.SwarmHash = await gatewayService.ResolveSwarmAddressToHashAsync(swarmAddress.Value);
            
            switch (Path.GetExtension(mainFile.FileName).ToLowerInvariant())
            {
                //hls
                case ".m3u8":
                {
                    var masterPlaylist = await parsingService.TryParseHlsMasterPlaylistFromFileAsync(mainFile);
                    
                    //if is a master playlist
                    if (masterPlaylist is not null) 
                        return await parsingService.ParseVideoEncodingFromHlsMasterPlaylistFileAsync(
                            ffProbeResult.Format.Duration,
                            mainFile,
                            swarmAddress,
                            masterPlaylist);
                    
                    //else, this is a single stream playlist
                    var variant = await parsingService.ParseVideoVariantFromHlsStreamPlaylistFileAsync(
                        mainFile,
                        swarmAddress,
                        ffProbeResult.Streams.First(s => s.Height != 0).Height,
                        ffProbeResult.Streams.First(s => s.Height != 0).Width);
                    return new HlsVideoEncoding(
                        ffProbeResult.Format.Duration,
                        masterFileDirectory.OriginalUri,
                        null,
                        [variant]);
                }
                
                //mp4
                case ".mp4":
                    return new Mp4VideoEncoding(
                        ffProbeResult.Format.Duration,
                        masterFileDirectory.OriginalUri,
                        [
                            new SingleFileVideoVariant(
                                mainFile,
                                ffProbeResult.Streams.First(s => s.Height != 0).Height,
                                ffProbeResult.Streams.First(s => s.Height != 0).Width)
                        ]
                    );
                
                //mpeg dash
                case ".mpd": throw new NotImplementedException();

                //all other encodings from a single file
                default:
                    return new UndefinedVideoEncoding(
                        ffProbeResult.Format.Duration,
                        masterFileDirectory.OriginalUri,
                        [
                            new SingleFileVideoVariant(
                                mainFile,
                                ffProbeResult.Streams.First(s => s.Height != 0).Height,
                                ffProbeResult.Streams.First(s => s.Height != 0).Width)
                        ]
                    );
            }
        }
        
        public async Task<VideoEncodingBase> EncodeVideoAsync(
            VideoVariantBase inputVideoVariant,
            int[] outputHeights,
            VideoType outputType,
            string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            
            // Compose FFmpeg command args.
            TimeSpan? duration = default;
            List<string> args;
            string? masterPlaylistPath = default;
            (string filePath, int height, int width)[] outputVariantRefs;
            switch (outputType)
            {
                case VideoType.Dash: throw new NotImplementedException();
                case VideoType.Hls:
                    args = BuildHlsFFmpegCommandArgs(
                        inputVideoVariant,
                        outputHeights,
                        outputDirectory,
                        out masterPlaylistPath,
                        out outputVariantRefs);
                    break;
                case VideoType.Mp4:
                    args = BuildMp4FFmpegCommandArgs(
                        inputVideoVariant,
                        outputHeights,
                        outputDirectory,
                        out outputVariantRefs);
                    break;
                default:
                    throw new InvalidOperationException($"Can't encode to {outputType}");
            }

            ioService.WriteLine($"Encoding variants [{outputVariantRefs.Select(
                o => o.height.ToString(CultureInfo.InvariantCulture)).Aggregate((a, h) => $"{a}, {h}")}]...");

            // Run FFmpeg command.
            var command = Command.Run(await GetFFmpegBinaryPathAsync(), args);

            activedCommands.Add(command);
            ioService.CancelKeyPress += ManageInterrupted;

            // Print filtered console output.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() =>
            {
                /*
                 * WTF! It works as an async method.
                 * The enumerable is built on top of a "while(true)" that ends when the process is stopped.
                 */
                foreach (var line in command.GetOutputAndErrorLines())
                {
                    var trimmedLine = line.TrimStart();
                    if (trimmedLine.StartsWith("frame=", StringComparison.InvariantCulture))
                        ioService.Write(line + '\r');
                    else if (duration is null)
                    {
                        var durationMatch = DurationRegex().Match(trimmedLine);
                        if (durationMatch.Success)
                        {
                            var durationStr = durationMatch.Groups["d"].Value;
                            duration = TimeSpan.Parse(durationStr, CultureInfo.InvariantCulture);
                        }
                    }
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            // Waiting until end and stop console output.
            var result = await command.Task;

            ioService.Write(new string(' ', ioService.BufferWidth));
            ioService.SetCursorPosition(0, ioService.CursorTop - 1);
            ioService.WriteLine(null, false);

            // Validate result.
            if (!result.Success)
                throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
            if (!duration.HasValue)
                throw new InvalidOperationException($"Didn't identify duration from ffmpeg outputs");

            switch (outputType)
            {
                case VideoType.Dash:
                    throw new NotImplementedException();
                case VideoType.Hls:
                {
                    List<HlsVideoVariant> variants = [];
                    foreach (var varRef in outputVariantRefs)
                    {
                        var streamPlaylistFile = await FileBase.BuildFromUFileAsync(
                            uFileProvider.BuildNewUFile(new BasicUUri(varRef.filePath, UUriKind.LocalAbsolute)));
                        variants.Add(await parsingService.ParseVideoVariantFromHlsStreamPlaylistFileAsync(
                            streamPlaylistFile,
                            null,
                            varRef.height,
                            varRef.width));
                    }
                    return new HlsVideoEncoding(
                        duration.Value,
                        outputDirectory,
                        await FileBase.BuildFromUFileAsync(uFileProvider.BuildNewUFile(new BasicUUri(
                            masterPlaylistPath ?? throw new InvalidOperationException("Master playlist path can't be null here"),
                            UUriKind.LocalAbsolute))),
                        variants.ToArray());
                }
                case VideoType.Mp4:
                {
                    List<SingleFileVideoVariant> variants = [];
                    foreach (var varRef in outputVariantRefs)
                    {
                        var variantFile = await FileBase.BuildFromUFileAsync(
                            uFileProvider.BuildNewUFile(new BasicUUri(varRef.filePath, UUriKind.LocalAbsolute)));
                        variants.Add(new SingleFileVideoVariant(variantFile, varRef.height, varRef.width));
                    }
                    return new Mp4VideoEncoding(
                        duration.Value,
                        outputDirectory,
                        variants.ToArray());
                }
                default:
                    throw new InvalidOperationException($"Can't encode to {outputType}");
            }
        }
        
        public async Task<string> ExtractThumbnailAsync(VideoVariantBase inputVideoVariant)
        {
            ioService.WriteLine($"Extract random thumbnail");

            // Run FFmpeg command.
            var outputThumbnailFilePath = Path.Combine(
                CommonConsts.TempDirectory.FullName,
                $"input_{inputVideoVariant.Width}x{inputVideoVariant.Height}.jpg");
            var args = new[] {
                "-i", inputVideoVariant.EntryFile.UUri.ToAbsoluteUri().OriginalUri,
                "-vf", "select='eq(pict_type\\,I)',random",
                "-vframes", "1",
                outputThumbnailFilePath
            };
            var command = Command.Run(await GetFFmpegBinaryPathAsync(), args.Cast<object>());

            activedCommands.Add(command);
            ioService.CancelKeyPress += ManageInterrupted;

            // Waiting until end and stop console output.
            var result = await command.Task;

            // Inspect and print result.
            if (!result.Success)
                throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");

            return outputThumbnailFilePath;
        }

        public async Task<string> GetFFmpegBinaryPathAsync() =>
            ffMpegBinaryPath ??=
                await TryFindBinaryPathAsync(
                     options.CustomFFmpegFolderPath,
                     CommonConsts.DefaultFFmpegFolder,
                     CommonConsts.FFmpegBinaryName,
                     "-version") ??
                throw new InvalidOperationException($"{CommonConsts.FFmpegBinaryName} not found");

        public async Task<string> GetFFprobeBinaryPathAsync() =>
            ffProbeBinaryPath ??=
                await TryFindBinaryPathAsync(
                    options.CustomFFmpegFolderPath,
                    CommonConsts.DefaultFFmpegFolder,
                    CommonConsts.FFprobeBinaryName,
                    "-version") ??
                throw new InvalidOperationException($"{CommonConsts.FFprobeBinaryName} not found");

        public async Task<FFProbeResultDto> GetVideoInfoAsync(string videoFileAbsoluteUri)
        {
            var args = new[] {
                $"-v", "error",
                "-show_entries", "format=duration",
                "-show_entries", "stream=width,height",
                "-of", "json",
                "-sexagesimal",
                videoFileAbsoluteUri};

            var command = Command.Run(await GetFFprobeBinaryPathAsync(), args.Cast<object>());
            command.Wait();
            var result = command.Result;

            // Inspect result.
            if (!result.Success)
                throw new InvalidOperationException($"ffprobe command failed with exit code {result.ExitCode}: {result.StandardError}");

            var ffProbeResult = JsonSerializer.Deserialize<FFProbeResultDto>(
                                    result.StandardOutput.Trim(),
                                    jsonSerializerOptions)
                                ?? throw new InvalidDataException($"FFProbe result have an invalid json");

            /*
             * ffProbe return even an empty element in Streams
             * Take the right resolution with OrderByDescending
             */
            ffProbeResult.Streams = new[] { ffProbeResult.Streams.OrderByDescending(s => s.Height).First() };

            return ffProbeResult;
        }

        // Helpers.
        [SuppressMessage("ReSharper", "AppendToCollectionExpression")]
        private List<string> BuildHlsFFmpegCommandArgs(
            VideoVariantBase inputVariant,
            int[] outputHeights,
            string outputDirectory,
            out string masterPlaylistPath,
            out (string filePath, int height, int width)[] outputVariantRefs)
        {
            // Define resolutions.
            var resolutionRatio = (decimal)inputVariant.Width / inputVariant.Height;
            var outputResolutions = outputHeights
                .Where(height => height <= inputVariant.Height) //don't upscale
                .Select(height =>
                {
                    var scaledWidth = (int)Math.Round(height * resolutionRatio, 0);
                    switch (scaledWidth % 4)
                    {
                        case 1: scaledWidth--; break;
                        case 2: scaledWidth += 2; break;
                        case 3: scaledWidth++; break;
                    }

                    return (height, width: scaledWidth);
                }).ToArray();
            
            // Report references.
            masterPlaylistPath = Path.Combine(outputDirectory, HlsMasterPlaylistName);
            outputVariantRefs = outputResolutions.Select(res => (
                Path.Combine(outputDirectory, $"{res.height}p", HlsStreamPlaylistName),
                res.height,
                res.width)).ToArray();
            
            // Build FFmpeg args.
            List<string> args = [];

            args.Add("-i"); args.Add(inputVariant.EntryFile.UUri.ToAbsoluteUri().OriginalUri);  //input
            foreach (var _ in outputResolutions)                                                //map input to streams
            {
                args.Add("-map"); args.Add("0:v:0");
                args.Add("-map"); args.Add("0:a:0");
            }
            for (int i = 0; i < outputResolutions.Length; i++)                                  //build output streams
            {
                args.Add($"-s:v:{i}"); args.Add($"{outputResolutions[i].width}x{outputResolutions[i].height}");
                args.Add($"-b:v:{i}"); args.Add(GetHlsH264BitrateArg(outputResolutions[i].height, outputResolutions[i].width));
            }
            args.Add("-preset"); args.Add(options.PresetCodec.ToString().ToLowerInvariant());   //preset
            args.Add("-c:a"); args.Add("aac");                                                  //audio codec
            args.Add("-c:v"); args.Add("libx264");                                              //video codec
            args.Add("-f"); args.Add("hls");                                                    //hls format
            args.Add("-hls_time"); args.Add("6");                                               //segment duration
            args.Add("-hls_list_size"); args.Add("0");                                          //keep all segments
            args.Add("-hls_playlist_type"); args.Add("vod");                                    //video on demand
            args.Add("-hls_segment_filename"); args.Add(Path.Combine(outputDirectory, "%v/%d.ts"));//segments filename
            args.Add("-var_stream_map");                                                        //map output streams
            args.Add(string.Join(' ', outputResolutions.Select(
                (res, i) => $"v:{i},a:{i},name:{res.height}p")));
            args.Add("-master_pl_name"); args.Add(HlsMasterPlaylistName);                       //master playlist name
            args.Add(Path.Combine(outputDirectory, "%v", HlsStreamPlaylistName));               //stream playlist names
            
            return args;
        }

        /// <summary>
        /// Build FFmpeg command args to encode current source video
        /// </summary>
        /// <param name="inputVariant">Input video variant</param>
        /// <param name="outputHeights">Required output video heights</param>
        /// <param name="outputDirectory">The destination directory for encoded files</param>
        /// <param name="outputVariantRefs">Output video variant references</param>
        /// <returns>FFmpeg args list</returns>
        private List<string> BuildMp4FFmpegCommandArgs(
            VideoVariantBase inputVariant,
            int[] outputHeights,
            string outputDirectory,
            out (string filePath, int height, int width)[] outputVariantRefs)
        {
            // Define resolutions.
            var resolutionRatio = (decimal)inputVariant.Width / inputVariant.Height;
            var outputResolutions = outputHeights
                .Where(height => height <= inputVariant.Height) //don't upscale
                .Select(height =>
                {
                    var scaledWidth = (int)Math.Round(height * resolutionRatio, 0);
                    switch (scaledWidth % 4)
                    {
                        case 1: scaledWidth--; break;
                        case 2: scaledWidth += 2; break;
                        case 3: scaledWidth++; break;
                    }

                    return (height, width: scaledWidth);
                });
            
            // Build FFmpeg args.
            var args = new List<string>();
            var outputVariantRefsList = new List<(string filePath, int height, int width)>();
            
            args.Add("-i"); args.Add(inputVariant.EntryFile.UUri.ToAbsoluteUri().OriginalUri);      //input

            //all output variants
            foreach (var (height, width) in outputResolutions)
            {
                args.Add("-c:a"); args.Add("aac");                                                  //audio codec
                args.Add("-c:v"); args.Add("libx264");                                              //video codec
                args.Add("-preset"); args.Add(options.PresetCodec.ToString().ToLowerInvariant());   //preset
                args.Add("-movflags"); args.Add("faststart");                                       //flags
                args.Add("-vf");                                                                    //filters
                {
                    string[] filters = [$"scale=w={width}:h={height}"];
                    args.Add($"{filters.Aggregate((r, f) => $"{r},{f}")}");
                }
                var outputFilePath = Path.Combine(outputDirectory, $"{height}.mp4"); //output
                args.Add(outputFilePath);

                // Add output info.
                outputVariantRefsList.Add((outputFilePath, height, width));
            }

            // Return values.
            outputVariantRefs = outputVariantRefsList.ToArray();
            return args;
        }

        private string GetHlsH264BitrateArg(int height, int width)
        {
            int FindBitrateValue(int area)
            {
                //exact match, return it
                if (HlsBitrateByArea.TryGetValue(area, out int value))
                    return value;
            
                //if area is bigger than any in table, extend bitrate proportionally with last value
                var maxKey = HlsBitrateByArea.Keys.Max();
                if (maxKey < area)
                    return area * HlsBitrateByArea[maxKey] / maxKey;
            
                //else, create linear interpolation between prev and next value
                var floorKey = HlsBitrateByArea.Keys.OrderDescending().First(k => k < area);
                var ceilingKey = HlsBitrateByArea.Keys.Order().First(k => k > area);

                return (int)(((long)HlsBitrateByArea[ceilingKey] - HlsBitrateByArea[floorKey]) * (area - floorKey) /
                    (ceilingKey - floorKey) + HlsBitrateByArea[floorKey]);
            }

            var bitrate = FindBitrateValue(height * width);
            return bitrate + "k";
        }

        /// <summary>
        /// Check for existing binary file from custom folder, fallback folder or global path
        /// </summary>
        /// <param name="customFolderPath">Optional custom folder path</param>
        /// <param name="fallbackFolderPath">A default fallback folder path</param>
        /// <param name="binaryName">Binary file to check</param>
        /// <param name="binaryTestArgs">Optional binary args to test with global invoke</param>
        /// <returns>The binary path, null if it doesn't exist</returns>
        private static async Task<string?> TryFindBinaryPathAsync(
            string? customFolderPath,
            string fallbackFolderPath,
            string binaryName,
            params object[] binaryTestArgs)
        {
            // If present, take custom folder and ignore others.
            if (customFolderPath is not null)
            {
                var customBinaryPath = Path.Combine(customFolderPath, binaryName);
                return File.Exists(customBinaryPath) ?
                    customBinaryPath : null;
            }
            
            // Else, test fallback folder.
            var fallbackBinaryPath = Path.Combine(fallbackFolderPath, binaryName);
            if (File.Exists(fallbackBinaryPath))
                return fallbackBinaryPath;

            // Test for global binary.
            try
            {
                var command = Command.Run(binaryName, binaryTestArgs);
                var result = await command.Task;
                if (result.Success)
                    return binaryName;
            }
            catch (Win32Exception) { }

            // Not found.
            return null;
        }

        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args) =>
            activedCommands.ForEach(c => c.Kill());
    }
}
