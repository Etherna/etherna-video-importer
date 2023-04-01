using Etherna.VideoImporter.Core.Models.Domain;
using Medallion.Shell;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Etherna.VideoImporter.Core.Services
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public partial class EncoderService : IEncoderService
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        // Fields.
        private readonly string ffMpegBinaryPath;
        private readonly IEnumerable<int> supportedResolutions;
        private MemoryStream? memoryStream;
        private MemoryStream? memoryStream2;

        // Constructor.
        public EncoderService(
            string ffMpegBinaryPath,
            IEnumerable<int> supportedResolutions)
        {
            this.ffMpegBinaryPath = ffMpegBinaryPath;
            this.supportedResolutions = supportedResolutions;
        }

        // Methods.
        public async Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile videoLocalFile,
            AudioLocalFile audioLocalFile,
            DirectoryInfo importerTempDirectoryInfo)
        {
            if (videoLocalFile is null)
                throw new ArgumentNullException(nameof(videoLocalFile));
            if (audioLocalFile is null)
                throw new ArgumentNullException(nameof(audioLocalFile));
            if (importerTempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(importerTempDirectoryInfo));


            var videoEncoded = new List<VideoLocalFile>();
            var fileNameGuid = Guid.NewGuid().ToString();
            var resolutionRatio = Math.Round((decimal)videoLocalFile.Width / videoLocalFile.Height, 5);
            foreach (var heightResolution in supportedResolutions)
            {
                var onlyMux = false;
                if (videoLocalFile.Height < heightResolution)
                    continue;
                else if (videoLocalFile.Height == heightResolution)
                    onlyMux = true;

                Console.WriteLine($"Processing resolution {heightResolution} in progress...");

                // Get scaled height
                var scaledWidth = (int)Math.Round(heightResolution * resolutionRatio, 0);
                if (scaledWidth % 2 != 0)
                    scaledWidth++;

                var fileName = $"{importerTempDirectoryInfo.FullName}/{fileNameGuid}_{(onlyMux ? "Muxed" : "Transcoded")}_{heightResolution}.mp4";
                var outputLines = new List<string>();
                var args = new string[] {"-i",
                    audioLocalFile.FilePath,
                    "-i",
                    videoLocalFile.FilePath,
                    "-c:a",
                    "aac",
                    "-c:v",
                    "libx264",
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
                    "-loglevel",
                    "info"};
                var command = Command.Run(
                    ffMpegBinaryPath,
                    args.Where(arg => !string.IsNullOrWhiteSpace(arg)));
#pragma warning disable CA2000 // Dispose objects before losing scope
                memoryStream2 = new MemoryStream();
                command.RedirectTo(memoryStream2);
                command.RedirectFrom(Console.OpenStandardInput());
                memoryStream = new MemoryStream();
                command.RedirectStandardErrorTo(memoryStream);
#pragma warning restore CA2000 // Dispose objects before losing scope
                /*if (received?.Data is not null &&
                        received.Data.StartsWith("frame=", StringComparison.InvariantCulture))
                {
                    Console.WriteLine(received.Data);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }*/
                using System.Timers.Timer aTimer = new();
                aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                aTimer.Interval = 5000;
                aTimer.Enabled = true;
                var result = await command.Task;
                aTimer.Stop();
                await memoryStream.DisposeAsync();
                using System.Timers.Timer aTimer2 = new();
                aTimer2.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                aTimer2.Interval = 5000;
                aTimer2.Enabled = true;
                var result2 = await command.Task;
                aTimer2.Stop();
                await memoryStream2.DisposeAsync();
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

        private void OnTimedEvent(object? source, ElapsedEventArgs e)
        {
            if (memoryStream is null ||
                !memoryStream.CanRead)
                return;
            memoryStream.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new StreamReader(memoryStream);
            string text = reader.ReadToEnd();
            Console.WriteLine(text);
        }

        private void OnTimedEvent2(object? source, ElapsedEventArgs e)
        {
            if (memoryStream2 is null)
                return;
            memoryStream2.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new StreamReader(memoryStream2);
            string text = reader.ReadToEnd();
            Console.WriteLine(text);
        }
    }
}
