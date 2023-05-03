using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.LocalVideoDto;
using Etherna.VideoImporter.Models.LocalVideoDtos;
using Medallion.Shell;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    public sealed class LocalVideosProvider : IVideoProvider
    {
        // Fields.
        private readonly string ffProbeBinaryPath;
        private readonly IEncoderService encoderService;
        private readonly bool includeAudioTrack;
        private readonly string jsonMetadataFilePath;
        private readonly IEnumerable<int> supportedHeightResolutions;
        private readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // Constructor.
        public LocalVideosProvider(
            string jsonMetadataFilePath,
            IEncoderService encoderService,
            bool includeAudioTrack,
            IEnumerable<int> supportedHeightResolutions,
            string ffProbeBinaryPath)
        {
            this.jsonMetadataFilePath = jsonMetadataFilePath;
            this.encoderService = encoderService;
            this.includeAudioTrack = includeAudioTrack;
            this.supportedHeightResolutions = supportedHeightResolutions;
            this.ffProbeBinaryPath = ffProbeBinaryPath;
        }

        // Properties.
        public string SourceName => jsonMetadataFilePath;

        // Methods.
        public async Task<Video> GetVideoAsync(
            VideoMetadataBase videoMetadata,
            DirectoryInfo tempDirectory)
        {
            if (tempDirectory is null)
                throw new ArgumentNullException(nameof(tempDirectory));

            var localVideoMetadata = videoMetadata as LocalVideoMetadata 
                ?? throw new ArgumentException($"Metadata must be of type {nameof(LocalVideoMetadata)}", nameof(videoMetadata));

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(
                localVideoMetadata.SourceVideo,
                tempDirectory,
                supportedHeightResolutions,
                includeAudioTrack);

            // Transcode thumbnail resolutions.
            var thumbnailFiles = localVideoMetadata.SourceThumbnail is not null ?
                await localVideoMetadata.SourceThumbnail.GetScaledThumbnailsAsync(tempDirectory) :
                Array.Empty<ThumbnailLocalFile>();

            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var localVideosMetadataDto = JsonSerializer.Deserialize<List<LocalVideoMetadataDto>>(
                await File.ReadAllTextAsync(jsonMetadataFilePath), options) 
                ?? throw new InvalidDataException($"LocalFile wrong format in {jsonMetadataFilePath}");

            var videosMetadataDictionary = new Dictionary<string, VideoMetadataBase>();
            foreach (var metadataDto in localVideosMetadataDto)
            {
                if (videosMetadataDictionary.ContainsKey(metadataDto.Id))
                    throw new InvalidOperationException($"Duplicate video Id found: {metadataDto.Id}");

                try
                {
                    // Get thumbnail info.
                    ThumbnailLocalFile? thumbnail = null;
                    if (!string.IsNullOrWhiteSpace(metadataDto.ThumbnailFilePath))
                    {
                        using var thumbFileStream = File.OpenRead(metadataDto.ThumbnailFilePath);
                        using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                        using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                        thumbnail = new ThumbnailLocalFile(metadataDto.ThumbnailFilePath, thumbBitmap.ByteCount, thumbBitmap.Height, thumbBitmap.Width);
                    }

                    // Get video info.
                    var ffProbeResult = GetFFProbeVideoInfo(metadataDto.VideoFilePath);

                    videosMetadataDictionary.Add(
                        metadataDto.Id,
                        new LocalVideoMetadata(
                            metadataDto.Id,
                            metadataDto.Title,
                            metadataDto.Description,
                            ffProbeResult.Format.Duration,
                            $"{ffProbeResult.Streams.First().Height}p",
                            thumbnail,
                            new VideoLocalFile(
                                metadataDto.VideoFilePath,
                                ffProbeResult.Streams.First().Height,
                                ffProbeResult.Streams.First().Width,
                                new FileInfo(metadataDto.VideoFilePath).Length)));

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

            Console.WriteLine($"Found {videosMetadataDictionary.Count} videos");

            return videosMetadataDictionary.Values;
        }

        // Helpers.
        private FFProbeResultDto GetFFProbeVideoInfo(string videoFilePath)
        {
            var args = new string[] {
                $"-v", "error",
                "-show_entries", "format=duration",
                "-show_entries", "stream=width,height",
                "-of", "json",
                "-sexagesimal",
                videoFilePath};

            var command = Command.Run(ffProbeBinaryPath, args);
            command.Wait();
            var result = command.Result;

            // Inspect result.
            if (!result.Success)
                throw new InvalidOperationException($"ffprobe command failed with exit code {result.ExitCode}: {result.StandardError}");

            var ffProbeResult = JsonSerializer.Deserialize<FFProbeResultDto>(result.StandardOutput.Trim(), options)
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
