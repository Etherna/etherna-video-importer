using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncoderService : IEncoderService
    {
        // Consts.
        private readonly IEnumerable<int> SupportedHeightResolutions = new[] { 360, 480, 720, 1080, 1440 };

        // Fields.
        private readonly List<Command> activedCommands = new();
        private readonly IFFmpegService ffMpegService;
        private readonly EncoderServiceOptions options;

        // Constructor.
        public EncoderService(
            IFFmpegService ffMpegService,
            IOptions<EncoderServiceOptions> options)
        {
            this.options = options.Value;
            this.ffMpegService = ffMpegService;
        }

        // Methods.
        public async Task<IEnumerable<VideoSourceFile>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile)
        {
            if (sourceVideoFile is null)
                throw new ArgumentNullException(nameof(sourceVideoFile));

            var videoEncodedFiles = new List<VideoSourceFile>();

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

            foreach (var (outputFilePath, outputHeight, outputWidth) in outputs)
            {
                var outputFileSize = new FileInfo(outputFilePath).Length;
                videoEncodedFiles.Add(VideoSourceFile.BuildNew(outputFilePath, ffMpegService));

                Console.WriteLine($"Encoded output stream {outputHeight}:{outputWidth}, file size: {outputFileSize} byte");
            }

            // Remove all video encodings where exists another with greater resolution, and equal or less file size.
            await RemoveUnusefulResolutionsAsync(videoEncodedFiles);

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
            VideoSourceFile sourceVideoFile,
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

        private static async Task RemoveUnusefulResolutionsAsync(List<VideoSourceFile> videoFiles)
        {
            var videoFilesWithByteSize = new List<(VideoSourceFile video, long byteSize)>();
            foreach (var file in videoFiles)
                videoFilesWithByteSize.Add((file, await file.GetByteSizeAsync()));

            videoFilesWithByteSize.RemoveAll(
                vf1 => videoFilesWithByteSize.Any(
                    vf2 => vf1.video.Height < vf2.video.Height &&
                           vf1.byteSize >= vf2.byteSize));

            videoFiles.Clear();
            videoFiles.AddRange(videoFilesWithByteSize.Select(p => p.video));
        }
    }
}
