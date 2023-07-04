using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.LocalVideoDto;
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
    internal sealed class JsonListVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IEncoderService encoderService;
        private readonly JsonListVideoProviderOptions options;

        // Constructor.
        public JsonListVideoProvider(
            IEncoderService encoderService,
            IOptions<JsonListVideoProviderOptions> options)
        {
            this.encoderService = encoderService;
            this.options = options.Value;
        }

        // Properties.
        public string SourceName => options.JsonMetadataUri.OriginalUri;

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
                Array.Empty<ThumbnailSourceFile>();

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            // Read json list.
            string jsonData = await new GenericSourceFile(options.JsonMetadataUri).ReadAsStringAsync();
            string jsonMetadataDirectoryAbsoluteUri = (options.JsonMetadataUri.TryGetParentDirectoryAsAbsoluteUri() ??
                throw new InvalidOperationException("Must exist a parent directory")).Item1;

            // Parse json video list.
            var localVideosMetadataDto = JsonSerializer.Deserialize<List<LocalVideoMetadataDto>>(jsonData) 
                ?? throw new InvalidDataException("Invalid Json metadata");

            var videosMetadataDictionary = new Dictionary<string, VideoMetadataBase>();
            foreach (var metadataDto in localVideosMetadataDto)
            {
                if (videosMetadataDictionary.ContainsKey(metadataDto.Id))
                    throw new InvalidOperationException($"Duplicate video Id found: {metadataDto.Id}");

                try
                {
                    // Get thumbnail info.
                    ThumbnailSourceFile? thumbnail = null;
                    if (!string.IsNullOrWhiteSpace(metadataDto.ThumbnailFilePath))
                    {
                        var absoluteThumbnailFilePath = Path.IsPathFullyQualified(metadataDto.ThumbnailFilePath) ?
                            metadataDto.ThumbnailFilePath :
                            Path.Combine(jsonMetadataDirectoryAbsoluteUri, metadataDto.ThumbnailFilePath);

                        using var thumbFileStream = File.OpenRead(absoluteThumbnailFilePath);
                        using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                        using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                        thumbnail = new ThumbnailSourceFile(absoluteThumbnailFilePath, thumbBitmap.ByteCount, thumbBitmap.Height, thumbBitmap.Width);
                    }

                    // Get video info.
                    var absoluteVideoFilePath = new SourceUri(metadataDto.VideoFilePath).ToAbsoluteUri(baseDirectory: jsonMetadataDirectoryAbsoluteUri);
                    var ffProbeResult = GetFFProbeVideoInfo(absoluteVideoFilePath);

                    videosMetadataDictionary.Add(
                        metadataDto.Id,
                        new LocalVideoMetadata(
                            metadataDto.Id,
                            metadataDto.Title,
                            metadataDto.Description,
                            ffProbeResult.Format.Duration,
                            $"{ffProbeResult.Streams.First().Height}p",
                            thumbnail,
                            new VideoSourceFile(
                                absoluteVideoFilePath,
                                ffProbeResult.Streams.First().Height,
                                ffProbeResult.Streams.First().Width,
                                ffProbeResult.Format.SizeLong))); //size here could be wrong in case of url pointing to HLS index file. In any case it's not needed.

                    Console.WriteLine($"Loaded metadata for {metadataDto.Title}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error importing video Id:{metadataDto.Id}.");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            return videosMetadataDictionary.Values;
        }

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;

        // Private helpers.
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
