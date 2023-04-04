using Etherna.VideoImporter.Core.Models.Domain;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public partial class EncoderService : IEncoderService
    {
        // Fields.
        private readonly string ffMpegBinaryPath;
        private readonly FFMpegHWAccelerationType ffMpegHWAccelerationType;
        private readonly IEnumerable<int> supportedHeightResolutions;
        private Command? command;

        // Constructor.
        public EncoderService(
            string ffMpegBinaryPath,
            FFMpegHWAccelerationType ffMpegHWAccelerationType,
            IEnumerable<int> supportedHeightResolutions)
        {
            this.ffMpegBinaryPath = ffMpegBinaryPath;
            this.ffMpegHWAccelerationType = ffMpegHWAccelerationType;
            this.supportedHeightResolutions = supportedHeightResolutions;
        }

        // Methods.
        public async Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile originalVideoLocalFile,
            AudioLocalFile originalAudioLocalFile,
            DirectoryInfo importerTempDirectoryInfo)
        {
            if (originalVideoLocalFile is null)
                throw new ArgumentNullException(nameof(originalVideoLocalFile));
            if (originalAudioLocalFile is null)
                throw new ArgumentNullException(nameof(originalAudioLocalFile));
            if (importerTempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(importerTempDirectoryInfo));

            var videoEncoded = new List<VideoLocalFile>();
            var fileNameGuid = Guid.NewGuid().ToString();
            var resolutionRatio = Math.Round((decimal)originalVideoLocalFile.Width / originalVideoLocalFile.Height, 5);
            foreach (var heightResolution in supportedHeightResolutions.Union(new List<int> { originalVideoLocalFile.Height }))
            {
                var onlyMux = false;
                if (originalVideoLocalFile.Height < heightResolution)
                    continue;
                else if (originalVideoLocalFile.Height == heightResolution)
                    onlyMux = true;

                Console.WriteLine($"Processing resolution {heightResolution} in progress...");

                // Get scaled height
                var scaledWidth = (int)Math.Round(heightResolution * resolutionRatio, 0);
                if (scaledWidth % 2 != 0)
                    scaledWidth++;

                var fileName = $"{importerTempDirectoryInfo.FullName}/{fileNameGuid}_{(onlyMux ? "Muxed" : "Transcoded")}_{heightResolution}.mp4";
                var outputLines = new List<string>();
                var args = new string[] {"-i",
                    originalAudioLocalFile.FilePath,
                    "-i",
                    originalVideoLocalFile.FilePath,
                    "-c:a",
                    "aac",
                    "-c:v",
                    ffMpegHWAccelerationType == FFMpegHWAccelerationType.None 
                        ? "libx264" : "h264_nvenc",
                    "-movflags",
                    "faststart",
                    "-sc_threshold",
                    "0",
                    "-r",
                    "25",
                    "-hls_time",
                    "2",
                    "-speed",
                    "6",
                    "-threads",
                    Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
                    fileName,
                    onlyMux ? "" : "-vf",
                    onlyMux ? "" : $"scale={scaledWidth}:{heightResolution}",
                    ffMpegHWAccelerationType == FFMpegHWAccelerationType.None
                        ? "" : "-hwaccel cuda -hwaccel_output_format cuda",
                    "-loglevel",
                    "info"};
                command = Command.Run(
                    ffMpegBinaryPath,
                    args.Where(arg => !string.IsNullOrWhiteSpace(arg))) ?? throw new InvalidOperationException($"Unable to start ffMpeg");
                Console.CancelKeyPress += new ConsoleCancelEventHandler(ManageInterrupted);
            
                // Print filtered console output.
                using var tokenSource = PrintConsoleStatus(command);

                // Waiting for end and stop console output.
                var result = await command.Task;
                tokenSource.Cancel();
                // inspect the result
                if (!result.Success)
                {
                    throw new InvalidOperationException($"command failed with exit code {result.ExitCode}: {result.StandardError}");
                }

                // Print result of ffMpeg
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine();
                var fileInfo = new FileInfo(fileName);
                if (!File.Exists(fileName) ||
                    fileInfo.Length <= 0)
                    throw new InvalidOperationException($"Some error when processing resolution {heightResolution}");
                Console.WriteLine($"Processing resolution {heightResolution} completed...");

                videoEncoded.Add(new VideoLocalFile(fileName, $"{heightResolution}p", heightResolution, scaledWidth, fileInfo.Length));
            }

            return videoEncoded;
        }

        // Helpers.
        private void ManageInterrupted(object? sender, ConsoleCancelEventArgs args)
        {
            if (command is null)
                return;
            if (args is null)
                return;

            Console.WriteLine();
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine("The operation has been interrupted.");
            args.Cancel = true;
            command.Kill();
        }

        private static CancellationTokenSource PrintConsoleStatus(Command command)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            var task = Task.Run(() =>
            {
                if (ct.IsCancellationRequested)
                    return;

                foreach (var line in command.GetOutputAndErrorLines())
                {
                    if (ct.IsCancellationRequested)
                        return;

                    if (line is not null &&
                        line.StartsWith("frame=", StringComparison.InvariantCulture))
                    {
                        Console.WriteLine(line);
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                    }
                }
            }, tokenSource.Token);
            return tokenSource;
        }
    }
}
