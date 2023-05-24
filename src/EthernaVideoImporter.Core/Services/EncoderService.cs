using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EncoderService : IEncoderService
    {
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
            var fileNameGuid = Guid.NewGuid();
            var resolutionRatio = (decimal)sourceVideoFile.Width / sourceVideoFile.Height;

            foreach (var heightResolution in options.GetSupportedHeightResolutions().Union(new List<int> { sourceVideoFile.Height }))
            {
                if (sourceVideoFile.Height < heightResolution)
                    continue;

                Console.WriteLine($"Encoding resolution {heightResolution}...");

                // Get scaled height
                var scaledWidth = (int)Math.Round(heightResolution * resolutionRatio, 0);
                var roundedScaledWidth = (scaledWidth % 4) switch
                {
                    0 => scaledWidth,
                    1 => scaledWidth - 1,
                    2 => scaledWidth + 2,
                    3 => scaledWidth + 1,
                    _ => throw new InvalidOperationException()
                };

                var fileName = $"{CommonConsts.TempDirectory.FullName}/{fileNameGuid}_{heightResolution}.mp4";
                var args = new string[] {
                    "-i", sourceVideoFile.FilePath,
                    "-c:a", "aac",
                    "-c:v", ffMpegHWAccelerationType switch
                    {
                        FFMpegHWAccelerationType.None => "libx264",
                        FFMpegHWAccelerationType.Cuda => "h264_nvenc"
                    },
                    ffMpegHWAccelerationType switch
                    {
                        FFMpegHWAccelerationType.None => "",
                        FFMpegHWAccelerationType.Cuda => "-hwaccel cuda -hwaccel_output_format cuda"
                    },
                    "-movflags", "faststart",
                    "-vf", $"scale={roundedScaledWidth}:{heightResolution}",
                    "-loglevel", "info",
                    fileName
                };

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

                // Waiting for end and stop console output.
                var result = await command.Task;

                // Inspect result.
                if (!result.Success)
                    throw new InvalidOperationException($"command failed with exit code {result.ExitCode}: {result.StandardError}");

                // Print result of ffMpeg
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine();
                Console.WriteLine($"Processing resolution {heightResolution} completed...");

                videoEncodedFiles.Add(new VideoLocalFile(fileName, heightResolution, roundedScaledWidth, new FileInfo(fileName).Length));
            }

            return videoEncodedFiles;
        }

        // Helpers.
        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args) =>
            activedCommands.ForEach(c => c.Kill());
    }
}
