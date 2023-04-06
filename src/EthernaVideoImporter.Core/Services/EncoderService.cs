﻿using Etherna.VideoImporter.Core.Models.Domain;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public partial class EncoderService : IEncoderService
    {
        // Fields.
        private readonly List<Command> activedCommands = new();

        // Constructor.
        public EncoderService(
            string ffMpegBinaryPath)
        {
            FFMpegBinaryPath = ffMpegBinaryPath;
        }

        // Properties.
        public string FFMpegBinaryPath { get; }

        // Methods.
        public async Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile originalVideoLocalFile,
            DirectoryInfo importerTempDirectoryInfo,
            IEnumerable<int> supportedHeightResolutions,
            bool includeAudioTrack)
        {
            if (originalVideoLocalFile is null)
                throw new ArgumentNullException(nameof(originalVideoLocalFile));
            if (importerTempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(importerTempDirectoryInfo));

            var videoEncodedFiles = new List<VideoLocalFile>();
            var fileNameGuid = Guid.NewGuid();
            var resolutionRatio = (decimal)originalVideoLocalFile.Width / originalVideoLocalFile.Height;

            foreach (var heightResolution in supportedHeightResolutions.Union(new List<int> { originalVideoLocalFile.Height }))
            {
                if (originalVideoLocalFile.Height < heightResolution)
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

                var fileName = $"{importerTempDirectoryInfo.FullName}/{fileNameGuid}_{heightResolution}.mp4";
                var args = new string[] {
                    $"-i {originalVideoLocalFile.FilePath}",
                    "-c:a aac",
                    "-c:v libx264",
                    "-movflags faststart",
                    $"-vf scale={roundedScaledWidth}:{heightResolution}",
                    "-loglevel info"};

                var command = Command.Run(FFMpegBinaryPath, args.SelectMany(arg => arg.Split(' ')).Append(fileName));

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
