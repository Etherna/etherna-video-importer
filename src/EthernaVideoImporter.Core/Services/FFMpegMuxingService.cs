using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
            IProgress<double>? progress,
            CancellationToken cancellationToken = default)
        {
            if (videoLocalFile is null)
                throw new ArgumentNullException(nameof(videoLocalFile));
            if (audioLocalFile is null)
                throw new ArgumentNullException(nameof(audioLocalFile));

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

            foreach (var transcode in supportedTranscodes)
            {
                Console.WriteLine($"Processing resolution {transcode.Key} in progress...");
                var procStartInfo = new ProcessStartInfo
                {
                    FileName = ffMpegBinaryPath,
                    Arguments = transcode.Value ?
                    $"-i \"{audioLocalFile.FilePath}\" -i \"{videoLocalFile.FilePath}\" -c:a aac -c:v libx265 -filter:v scale={transcode.Key}:-1 {Guid.NewGuid()}_Muxed_{transcode.Key}.mp4 -loglevel info" :
                    $"-i \"{audioLocalFile.FilePath}\" -i \"{videoLocalFile.FilePath}\" -c:a aac -c:v libx265 {Guid.NewGuid()}_Transcoded_{transcode.Key}.mp4 -loglevel info",

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
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new String(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new String(' ', Console.BufferWidth));
                Console.WriteLine();
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine($"Processing resolution {transcode.Key} completed...");
            }
            return new List<VideoLocalFile>();
        }
    }
}
