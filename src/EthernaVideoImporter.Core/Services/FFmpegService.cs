using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.FFmpegDto;
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
        private readonly FFmpegServiceOptions options;
        private string? ffMpegFolderPath;

        // Constructor.
        public FFmpegService(
            IOptions<FFmpegServiceOptions> options)
        {
            this.options = options.Value;
        }

        // Methods.
        public async Task<IEnumerable<(string filePath, int height, int width)>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile,
            IEnumerable<int> outputHeights)
        {
            // Compose FFmpeg command args.
            var args = BuildFFmpegCommandArgs(
                sourceVideoFile,
                outputHeights,
                out IEnumerable<(string filePath, int height, int width)> outputs);

            Console.WriteLine($"Encoding resolutions [{outputs.Select(o => o.height.ToString(CultureInfo.InvariantCulture)).Aggregate((r, h) => $"{r}, {h}")}]...");

            // Run FFmpeg command.
            var command = Command.Run(await GetFFmpegBinaryPathAsync(), args);

            activedCommands.Add(command);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ManageInterrupted);

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
                        Console.Write(line + '\r');
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            // Waiting until end and stop console output.
            var result = await command.Task;

            Console.Write(new string(' ', Console.BufferWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine();

            // Inspect and print result.
            if (!result.Success)
                throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
            return outputs;
        }

        public async Task<string> GetFFmpegBinaryPathAsync()
        {
            ffMpegFolderPath ??= await CommonCheckAndGetAsync(options.FFmpegFolderPath, CommonConsts.FFmpegBinaryName);
            if (ffMpegFolderPath is null)
                throw new NotImplementedException($"{CommonConsts.FFmpegBinaryName} not found");

            return Path.Combine(ffMpegFolderPath, CommonConsts.FFmpegBinaryName);
        }

        public async Task<string> GetFFprobeBinaryPathAsync()
        {
            ffMpegFolderPath ??= await CommonCheckAndGetAsync(options.FFmpegFolderPath, CommonConsts.FFprobeBinaryName);
            if (ffMpegFolderPath is null)
                throw new NotImplementedException($"{CommonConsts.FFprobeBinaryName} not found");

            return Path.Combine(ffMpegFolderPath, CommonConsts.FFprobeBinaryName);
        }

        public async Task<FFProbeResultDto> GetVideoInfoAsync(string videoFileAbsoluteUri)
        {
            var args = new string[] {
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
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
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
        /// <param name="sourceVideoFile">Input video file</param>
        /// <param name="outputs">Output video files info</param>
        /// <returns>FFmpeg args list</returns>
        private IEnumerable<string> BuildFFmpegCommandArgs(
            VideoSourceFile sourceVideoFile,
            IEnumerable<int> outputHeights,
            out IEnumerable<(string filePath, int height, int width)> outputs)
        {
            // Build FFmpeg args.
            var args = new List<string>();
            var fileNameGuid = Guid.NewGuid();
            var resolutionRatio = (decimal)sourceVideoFile.Width / sourceVideoFile.Height;
            var outputsList = new List<(string filePath, int height, int width)>();

            //input
            args.Add("-i"); args.Add(sourceVideoFile.FileUri.ToAbsoluteUri().Item1);

            //all output streams
            foreach (var height in outputHeights)
            {
                // Don't upscale, skip in case.
                if (sourceVideoFile.Height < height)
                    continue;

                // Build output stream args.
                var outputFilePath = Path.Combine(CommonConsts.TempDirectory.FullName, $"{fileNameGuid}_{height}.mp4");

                var scaledWidth = (int)Math.Round(height * resolutionRatio, 0);
                switch (scaledWidth % 4)
                {
                    case 1: scaledWidth--; break;
                    case 2: scaledWidth += 2; break;
                    case 3: scaledWidth++; break;
                    default: break;
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
        /// Check for exist file from custom folder or global path
        /// </summary>
        /// <param name="customFFmpegFolderPath">Custom folder or empty for global path</param>
        /// <param name="binaryName">Binary file to check</param>
        /// <returns>Null if not exist</returns>
        private async Task<string?> CommonCheckAndGetAsync(string customFFmpegFolderPath, string binaryName)
        {
            // Custom FFmpeg folder.
            if (!string.IsNullOrWhiteSpace(customFFmpegFolderPath))
            {
                if (File.Exists($"{customFFmpegFolderPath}/{binaryName}"))
                    return customFFmpegFolderPath;
            }

            // Global FFmpeg.
            try
            {
                var command = Command.Run(binaryName, "-version");
                var result = await command.Task;
                if (result.Success)
                    return "";
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Not found.
            return null;
        }

        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args) =>
            activedCommands.ForEach(c => c.Kill());
    }
}
