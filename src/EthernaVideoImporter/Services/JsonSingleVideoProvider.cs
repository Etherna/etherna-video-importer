using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.LocalVideoDtos;
using Etherna.VideoImporter.Options;
using Medallion.Shell;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class JsonSingleVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IEncoderService encoderService;
        private readonly JsonSingleVideoProviderOptions options;

        // Constructor.
        public JsonSingleVideoProvider(
            IEncoderService encoderService,
            IOptions<JsonSingleVideoProviderOptions> options)
        {
            this.encoderService = encoderService;
            this.options = options.Value;
        }

        // Properties.
        public string SourceName => "Console";

        // Methods.
        public async Task<Video> GetVideoAsync(
            VideoMetadataBase videoMetadata)
        {
            var localVideoMetadata = videoMetadata as LocalVideoMetadata 
                ?? throw new ArgumentException($"Metadata must be of type {nameof(LocalVideoMetadata)}", nameof(videoMetadata));

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(
                localVideoMetadata.SourceVideo);

            // Transcode thumbnail resolutions.
            var thumbnailFiles = localVideoMetadata.SourceThumbnail is not null ?
                await localVideoMetadata.SourceThumbnail.GetScaledThumbnailsAsync(CommonConsts.TempDirectory) :
                Array.Empty<ThumbnailLocalFile>();

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var videosMetadataDictionary = new Dictionary<string, VideoMetadataBase>();

            try
            {
                // Get thumbnail info.
                ThumbnailLocalFile? thumbnail = null;
                if (!string.IsNullOrWhiteSpace(options.Thumbnail))
                {
                    using var thumbFileStream = File.OpenRead(options.Thumbnail);
                    using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                    using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                    thumbnail = new ThumbnailLocalFile(options.Thumbnail, thumbBitmap.ByteCount, thumbBitmap.Height, thumbBitmap.Width);
                }

                // Get video info.
                var ffProbeResult = GetFFProbeVideoInfo(options.VideoUri);

                videosMetadataDictionary.Add(
                    options.VideoUri,
                    new LocalVideoMetadata(
                        options.VideoUri,
                        options.Title,
                        options.Description,
                        ffProbeResult.Format.Duration,
                        $"{ffProbeResult.Streams.First().Height}p",
                        thumbnail,
                        new VideoLocalFile(
                            options.VideoUri,
                            ffProbeResult.Streams.First().Height,
                            ffProbeResult.Streams.First().Width,
                            ffProbeResult.Format.SizeLong))); //size here could be wrong in case of url pointing to HLS index file. In any case it's not needed.

                Console.WriteLine($"Loaded metadata for {options.Title}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error importing video Id:{options.VideoUri}.");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            return await Task.FromResult(videosMetadataDictionary.Values);
        }

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;

        // Helpers.
        private FFProbeResultDto GetFFProbeVideoInfo(string videoFilePath)
        {
            var args = new string[] {
                $"-v", "error",
                "-show_entries", "format=duration,size",
                "-show_entries", "stream=width,height",
                "-of", "json",
                "-sexagesimal",
                videoFilePath};

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
