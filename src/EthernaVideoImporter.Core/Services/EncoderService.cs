using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncoderService : IEncoderService
    {
        // Consts.
        private readonly IEnumerable<int> SupportedHeightResolutions = new[] { 360, 480, 720, 1080, 1440 };

        // Fields.
        private readonly List<Command> activedCommands = new();
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IOptions<EncoderServiceOptions> options)
        {
            this.options = options.Value;
        }

        // Properties.
        public string FFMpegBinaryPath => options.FFMpegBinaryPath;

        // Methods.
        public async Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile sourceVideoFile)
        {
            if (sourceVideoFile is null)
                throw new ArgumentNullException(nameof(sourceVideoFile));

            var videoEncodedFiles = new List<VideoLocalFile>();

            // Compose FFmpeg command args.
            var args = BuildFFmpegCommandArgs(
                sourceVideoFile,
                out IEnumerable<(string filePath, int height, int width)> outputs);

            Console.WriteLine($"Encoding resolutions [{outputs.Select(o => o.height.ToString(CultureInfo.InvariantCulture)).Aggregate((r, h) => $"{r}, {h}")}]...");

            // Run FFmpeg command.
            var command = Command.Run(FFMpegBinaryPath, args);

            activedCommands.Add(command);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ManageInterrupted);

            // Print filtered console output.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() =>
            {
                PrintFFmpegEncodeInfo(command);
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

            foreach (var (outputFilePath, outputHeight, outputWidth) in outputs)
            {
                var outputFileSize = new FileInfo(outputFilePath).Length;
                videoEncodedFiles.Add(new VideoLocalFile(outputFilePath, outputHeight, outputWidth, outputFileSize));

                Console.WriteLine($"Encoded output stream {outputHeight}:{outputWidth}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            videoEncodedFiles.RemoveAll(vf1 => videoEncodedFiles.Any(vf2 => vf1.Height < vf2.Height &&
                                                                            vf1.ByteSize >= vf2.ByteSize));

            Console.WriteLine($"Keep [{videoEncodedFiles.Select(vf => vf.Height.ToString(CultureInfo.InvariantCulture)).Aggregate((r, h) => $"{r}, {h}")}] as valid resolutions to upload");

            return videoEncodedFiles;
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
            VideoLocalFile sourceVideoFile,
            out IEnumerable<(string filePath, int height, int width)> outputs)
        {
            // Build FFmpeg args.
            var args = new List<string>();
            var fileNameGuid = Guid.NewGuid();
            var resolutionRatio = (decimal)sourceVideoFile.Width / sourceVideoFile.Height;
            var outputsList = new List<(string filePath, int height, int width)>();

            //input
            args.Add("-i"); args.Add(sourceVideoFile.FilePath);

            //all output streams
            foreach (var height in SupportedHeightResolutions.Union(new List<int> { sourceVideoFile.Height })
                                                             .OrderDescending())
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

        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args) =>
            activedCommands.ForEach(c => c.Kill());

        private static void PrintFFmpegEncodeInfo(Command command)
        {
            /*
            * WTF! It works as an async method.
            * The enumerable is built on top of a "while(true)" that ends when the process is stopped.
            */
            int rowsStart = Console.CursorTop;
            var regex = new Regex(@"time=([\d:.]+)|speed=([\d.]+x)");
            foreach (var line in command.GetOutputAndErrorLines())
            {
                // Write info line.
                if (line.StartsWith("frame=", StringComparison.InvariantCulture))
                {
                    MatchCollection matches = regex.Matches(line);
                    if (matches.Count == 2)
                    {
                        // Clean the old lines info.
                        for (int i = 0; i < Console.CursorTop - rowsStart + 1; i++)
                        {
                            Console.SetCursorPosition(0, rowsStart + i);
                            Console.Write(new string(' ', Console.WindowWidth));
                            Console.Write('\r');
                        }

                        // Write new line updated.
                        foreach (Match match in matches)
                        {
                            if (match.Groups[1].Success)
                                Console.Write($"Time: {match.Groups[1].Value}\t");
                            else if (match.Groups[2].Success)
                                Console.Write($"Speed: {match.Groups[2].Value}\t");
                        }
                    }
                    Console.Write('\r');
                }
            }
        }
    }
}
