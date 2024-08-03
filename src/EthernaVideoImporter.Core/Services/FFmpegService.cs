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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.FFmpeg;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class FFmpegService : IFFmpegService
    {
        // Fields.
        private readonly List<Command> activedCommands = new();
        private readonly IIoService ioService;
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly FFmpegServiceOptions options;
        private string? ffMpegBinaryPath;
        private string? ffProbeBinaryPath;

        // Constructor.
        public FFmpegService(
            IIoService ioService,
            IOptions<FFmpegServiceOptions> options)
        {
            this.ioService = ioService;
            this.options = options.Value;
        }

        // Methods.
        public async Task<IEnumerable<(string filePath, int height, int width)>> EncodeVideosAsync(
            VideoFile videoFile,
            IEnumerable<int> outputHeights)
        {
            // Compose FFmpeg command args.
            var args = BuildFFmpegCommandArgs(
                videoFile,
                outputHeights,
                out IEnumerable<(string filePath, int height, int width)> outputs);

            ioService.WriteLine($"Encoding resolutions [{outputs.Select(o => o.height.ToString(CultureInfo.InvariantCulture)).Aggregate((r, h) => $"{r}, {h}")}]...");

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
                    if (line.StartsWith("frame=", StringComparison.InvariantCulture))
                        ioService.Write(line + '\r');
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            // Waiting until end and stop console output.
            var result = await command.Task;

            ioService.Write(new string(' ', ioService.BufferWidth));
            ioService.SetCursorPosition(0, ioService.CursorTop - 1);
            ioService.WriteLine(null, false);

            // Inspect and print result.
            if (!result.Success)
                throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
            return outputs;
        }
        
        public async Task<string> ExtractThumbnailAsync(VideoFile videoFile)
        {
            ioService.WriteLine($"Extract random thumbnail");

            // Run FFmpeg command.
            var outputThumbnailFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, $"{Guid.NewGuid()}_thumbnail.jpg");
            var args = new[] {
                "-i", videoFile.FileUri.ToAbsoluteUri().Item1,
                "-vf", "select='eq(pict_type\\,I)',random",
                "-vframes", "1",
                outputThumbnailFilePath
            };
            var command = Command.Run(await GetFFmpegBinaryPathAsync(), args);

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
                "-show_entries", "format=duration,size",
                "-show_entries", "stream=width,height",
                "-of", "json",
                "-sexagesimal",
                videoFileAbsoluteUri};

            var command = Command.Run(await GetFFprobeBinaryPathAsync(), args);
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
        private void AppendOutputFFmpegCommandArgs(List<string> args, int height, int width, string outputFilePath)
        {
            //audio codec
            args.Add("-c:a"); args.Add("aac");

            //video codec
            args.Add("-c:v"); args.Add("libx264");

            //preset
            args.Add("-preset"); args.Add(options.PresetCodec.ToString().ToLowerInvariant());

            //flags
            args.Add("-movflags"); args.Add("faststart");

            //filters
            args.Add("-vf");
            {
                var filters = new List<string>
                {
                    //scale
                    $"scale=w={width}:h={height}"
                };

                args.Add($"{filters.Aggregate((r, f) => $"{r},{f}")}");
            }

            //logs
            args.Add("-loglevel"); args.Add("info");

            //output
            args.Add(outputFilePath);
        }

        /// <summary>
        /// Build FFmpeg command args to encode current source video
        /// </summary>
        /// <param name="videoFile">Input video file</param>
        /// <param name="outputs">Output video files info</param>
        /// <returns>FFmpeg args list</returns>
        private List<string> BuildFFmpegCommandArgs(
            VideoFile videoFile,
            IEnumerable<int> outputHeights,
            out IEnumerable<(string filePath, int height, int width)> outputs)
        {
            // Build FFmpeg args.
            var args = new List<string>();
            var fileNameGuid = Guid.NewGuid();
            var resolutionRatio = (decimal)videoFile.Width / videoFile.Height;
            var outputsList = new List<(string filePath, int height, int width)>();

            //input
            args.Add("-i"); args.Add(videoFile.FileUri.ToAbsoluteUri().Item1);

            //all output streams
            foreach (var height in outputHeights)
            {
                // Don't upscale, skip in case.
                if (videoFile.Height < height)
                    continue;

                // Build output stream args.
                var outputFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, $"{fileNameGuid}_{height}.mp4");

                var scaledWidth = (int)Math.Round(height * resolutionRatio, 0);
                switch (scaledWidth % 4)
                {
                    case 1: scaledWidth--; break;
                    case 2: scaledWidth += 2; break;
                    case 3: scaledWidth++; break;
                }

                AppendOutputFFmpegCommandArgs(args, height, scaledWidth, outputFilePath);

                // Add output info.
                outputsList.Add((outputFilePath, height, scaledWidth));
            }

            // Return values.
            outputs = outputsList;
            return args;
        }

        /// <summary>
        /// Check for existing binary file from custom folder, fallback folder or global path
        /// </summary>
        /// <param name="customFolderPath">Optional custom folder path</param>
        /// <param name="fallbackFolderPath">A default fallback folder path</param>
        /// <param name="binaryName">Binary file to check</param>
        /// <param name="binaryTestArgs">Optional binary args to test with global invoke</param>
        /// <returns>The binary path, null if doesn't exist</returns>
        private static async Task<string?> TryFindBinaryPathAsync(string? customFolderPath, string fallbackFolderPath, string binaryName, params object[] binaryTestArgs)
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
            catch (System.ComponentModel.Win32Exception) { }

            // Not found.
            return null;
        }

        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args) =>
            activedCommands.ForEach(c => c.Kill());
    }
}
