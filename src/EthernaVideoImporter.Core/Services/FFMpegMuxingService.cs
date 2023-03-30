﻿using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Etherna.VideoImporter.Core.Services
{
    public partial class FFMpegMuxingService : IMuxingService
    {
        // Fields.
        private static readonly ReadOnlyCollection<int> SupportedResolutions = new(new List<int> { 1440, 1080, 720, 480, 360 });
        private readonly string ffMpegBinaryPath;

        // Constructor.
        public FFMpegMuxingService(string ffMpegBinaryPath)
        {
            this.ffMpegBinaryPath = ffMpegBinaryPath;
        }

        // Methods.
        public IEnumerable<VideoLocalFile> TranscodeVideos(
            VideoLocalFile videoLocalFile,
            AudioLocalFile audioLocalFile,
            DirectoryInfo importerTempDirectoryInfo,
            IProgress<double>? progress,
            CancellationToken cancellationToken = default)
        {
            if (videoLocalFile is null)
                throw new ArgumentNullException(nameof(videoLocalFile));
            if (audioLocalFile is null)
                throw new ArgumentNullException(nameof(audioLocalFile));
            if (importerTempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(importerTempDirectoryInfo));

            // Get video need transcoding or muxed video
            var supportedTranscodes = new Dictionary<int, bool>();
            foreach (var resolution in SupportedResolutions)
            {
                if (videoLocalFile.Height > resolution)
                    supportedTranscodes.Add(resolution, true);
                else if (videoLocalFile.Height == resolution)
                    supportedTranscodes.Add(resolution, false);
            }
            if (!supportedTranscodes.Any())
                throw new InvalidOperationException("Original video source don't support any transcoding resolutions");

            var fileNameGuid = Guid.NewGuid().ToString();
            var resolutionRatio = Math.Round((decimal)videoLocalFile.Width / videoLocalFile.Height, 5);
            foreach (var transcode in supportedTranscodes)
            {
                Console.WriteLine($"Processing resolution {transcode.Key} in progress...");

                // Get scaled height
                var scaledHeight = Math.Round(transcode.Key * resolutionRatio);
                if (scaledHeight % 2 != 0)
                    scaledHeight++;

                var fileName = $"{importerTempDirectoryInfo.FullName}/{fileNameGuid}_{(transcode.Value ? "Transcoded" : "Muxed")}_{transcode.Key}.mp4";
                var procStartInfo = new ProcessStartInfo
                {
                    FileName = ffMpegBinaryPath,
                    Arguments = transcode.Value ?
                    $"-i \"{audioLocalFile.FilePath}\" -i \"{videoLocalFile.FilePath}\" -c:a aac -c:v libx264 -movflags faststart -sc_threshold 0 -r 25 -hls_time 2 -speed 6 -vf scale={scaledHeight}:{transcode.Key} {fileName} -loglevel info" :
                    $"-i \"{audioLocalFile.FilePath}\" -i \"{videoLocalFile.FilePath}\" -c:a aac -c:v libx264 -movflags faststart -sc_threshold 0 -r 25 -hls_time 2 -speed 6 {fileName} -loglevel info",

                    // The following commands are needed to redirect the standard output.
                    // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    // Do not create the black window.
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                using var FFmpegProcess = Process.Start(procStartInfo) ?? throw new InvalidOperationException("null process");
                FFmpegProcess.OutputDataReceived += delegate (object sender, DataReceivedEventArgs received)
                {
                    Console.WriteLine(received.Data);
                };
                FFmpegProcess.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs received)
                {
                    if (received?.Data is not null &&
                        received.Data.StartsWith("frame=", StringComparison.InvariantCulture))
                    {
                        Console.WriteLine(received.Data);
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                    }
                };
                FFmpegProcess.BeginErrorReadLine();
                FFmpegProcess.WaitForExit();
                Console.Write(new String(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new String(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine();
                if (!File.Exists(fileName) ||
                    new FileInfo(fileName).Length <= 0)
                    throw new InvalidOperationException($"Some error when processing resolution {transcode.Key}");
                else
                    Console.WriteLine($"Processing resolution {transcode.Key} completed...");
            }
            return new List<VideoLocalFile>();
        }
    }
}
