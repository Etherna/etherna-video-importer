/*
 * 
 * Based on YoutubeExplode.Converter
 * https://github.com/Tyrrrz/YoutubeExplode/blob/master/License.txt
 * 
 */
using CliWrap;
using CliWrap.Builders;
using Etherna.VideoImporter.Core.Extensions;
using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode.Converter.Utils.Extensions;
using YoutubeExplode.Videos.Streams;

namespace Etherna.VideoImporter.Core.Services
{
    public partial class FFMpegMuxingService : IMuxingService
    {
        // Fields.
        private readonly string ffMpegBinaryPath;

        // Constructor.
        public FFMpegMuxingService(string ffMpegBinaryPath)
        {
            this.ffMpegBinaryPath = ffMpegBinaryPath;
        }

        // Methods.
        public async Task TranscodeVideoAsync(
            VideoLocalFile sourceVideo, 
            string resolutionTarget, 
            CancellationToken cancellationToken = default)
        {
            var arguments = new ArgumentsBuilder();

            // Stream inputs
            foreach (var streamInput in streamInputs)
                arguments.Add("-i").Add(streamInput.FilePath);

            // Subtitle inputs
            foreach (var subtitleInput in subtitleInputs)
                arguments.Add("-i").Add(subtitleInput.FilePath);

            // Input mapping
            for (var i = 0; i < streamInputs.Count + subtitleInputs.Count; i++)
                arguments.Add("-map").Add(i);

            // Format
            arguments.Add("-f").Add(container.Name);

            // Preset
            arguments.Add("-preset").Add(_preset);

            // Avoid transcoding where possible
            {
                var lastAudioStreamIndex = 0;
                var lastVideoStreamIndex = 0;
                foreach (var streamInput in streamInputs)
                {
                    // Note: a muxed stream input will map to two separate audio and video streams

                    if (streamInput.Info is IAudioStreamInfo audioStreamInfo)
                    {
                        if (audioStreamInfo.Container == container)
                        {
                            arguments
                                .Add($"-c:a:{lastAudioStreamIndex}")
                                .Add("copy");
                        }

                        lastAudioStreamIndex++;
                    }

                    if (streamInput.Info is IVideoStreamInfo videoStreamInfo)
                    {
                        if (videoStreamInfo.Container == container)
                        {
                            arguments
                                .Add($"-c:v:{lastVideoStreamIndex}")
                                .Add("copy");
                        }

                        lastVideoStreamIndex++;
                    }
                }
            }

            // MP4: specify the codec for subtitles manually, otherwise they may not get injected
            if (container == Container.Mp4 && subtitleInputs.Any())
                arguments.Add("-c:s").Add("mov_text");

            // MP3: set constant bitrate for audio streams, otherwise the metadata may contain invalid total duration
            // https://superuser.com/a/893044
            if (container == Container.Mp3)
            {
                var lastAudioStreamIndex = 0;
                foreach (var streamInput in streamInputs)
                {
                    if (streamInput.Info is IAudioStreamInfo audioStreamInfo)
                    {
                        arguments
                            .Add($"-b:a:{lastAudioStreamIndex++}")
                            .Add(Math.Round(audioStreamInfo.Bitrate.KiloBitsPerSecond) + "K");
                    }
                }
            }

            // Metadata for stream inputs
            {
                var lastAudioStreamIndex = 0;
                var lastVideoStreamIndex = 0;
                foreach (var streamInput in streamInputs)
                {
                    // Note: a muxed stream input will map to two separate audio and video streams

                    if (streamInput.Info is IAudioStreamInfo audioStreamInfo)
                    {
                        arguments
                            .Add($"-metadata:s:a:{lastAudioStreamIndex++}")
                            .Add($"title={audioStreamInfo.Bitrate}");
                    }

                    if (streamInput.Info is IVideoStreamInfo videoStreamInfo)
                    {
                        arguments
                            .Add($"-metadata:s:v:{lastVideoStreamIndex++}")
                            .Add($"title={videoStreamInfo.VideoQuality.Label} | {videoStreamInfo.Bitrate}");
                    }
                }
            }

            // Metadata for subtitles
            for (var i = 0; i < subtitleInputs.Count; i++)
            {
                arguments
                    .Add($"-metadata:s:s:{i}")
                    .Add($"language={subtitleInputs[i].Info.Language.Code}")
                    .Add($"-metadata:s:s:{i}")
                    .Add($"title={subtitleInputs[i].Info.Language.Name}");
            }

            // Misc settings
            arguments
                .Add("-threads").Add(Environment.ProcessorCount)
                .Add("-nostdin")
                .Add("-y");

            // Output
            arguments.Add(filePath);

            await ExecuteFFMpeg(arguments.Build(), progress, cancellationToken);
        }

        // Helpers.
        private static PipeTarget CreateProgressRouter(IProgress<double> progress)
        {
            var totalDuration = default(TimeSpan?);

            return PipeTarget.ToDelegate(l =>
            {
                totalDuration ??= DurationRegex().Match(l)
                    .Groups[1]
                    .Value
                    .NullIfWhiteSpace()?
                    .Pipe(s => TimeSpan.ParseExact(s, "c", CultureInfo.InvariantCulture));

                if (totalDuration is null || totalDuration == TimeSpan.Zero)
                    return;

                var processedDuration = TimeRegex().Match(l)
                    .Groups[1]
                    .Value
                    .NullIfWhiteSpace()?
                    .Pipe(s => TimeSpan.ParseExact(s, "c", CultureInfo.InvariantCulture));

                if (processedDuration is null)
                    return;

                progress.Report((
                    processedDuration.Value.TotalMilliseconds /
                    totalDuration.Value.TotalMilliseconds
                ).Clamp(0, 1));
            });
        }

        [GeneratedRegex("Duration:\\s(\\d\\d:\\d\\d:\\d\\d.\\d\\d)")]
        private static partial Regex DurationRegex();

        private async ValueTask ExecuteFFMpeg(
            string arguments,
            IProgress<double>? progress,
            CancellationToken cancellationToken = default)
        {
            var stdErrBuffer = new StringBuilder();

            var stdErrPipe = PipeTarget.Merge(
                // Collect error output in case of failure
                PipeTarget.ToStringBuilder(stdErrBuffer),
            // Collect progress output if requested
                progress?.Pipe(CreateProgressRouter) ?? PipeTarget.Null
            );

            var result = await Cli.Wrap(ffMpegBinaryPath)
                .WithArguments(arguments)
                .WithStandardErrorPipe(stdErrPipe)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"""
                FFmpeg exited with a non-zero exit code ({result.ExitCode}).
                Arguments:
                {arguments}
                Standard error:
                {stdErrBuffer}
                """
                );
            }
        }

        [GeneratedRegex("time=(\\d\\d:\\d\\d:\\d\\d.\\d\\d)")]
        private static partial Regex TimeRegex();
    }
}
