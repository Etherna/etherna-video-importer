using Etherna.VideoImporter.Core.Models.FFmpegDto;
using Etherna.VideoImporter.Core.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class FFmpegService : IFFmpegService
    {
        // Fields.
        private readonly FFmpegServiceOptions options;

        // Constructor.
        public FFmpegService(
            IOptions<FFmpegServiceOptions> options)
        {
            this.options = options.Value;
        }

        // Methods.
        public FFProbeResultDto GetFFProbeVideoInfo(string videoFileAbsoluteUri)
        {
            var args = new string[] {
                $"-v", "error",
                "-show_entries", "format=duration,size",
                "-show_entries", "stream=width,height",
                "-of", "json",
                "-sexagesimal",
                videoFileAbsoluteUri};

            var command = Command.Run(options.FFProbeBinaryPath, args);
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
    }
}
