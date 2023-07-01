﻿using Etherna.VideoImporter.Core;
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
    internal sealed class LocalVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IEncoderService encoderService;
        private readonly LocalVideoProviderOptions options;

        // Constructor.
        public LocalVideoProvider(
            IEncoderService encoderService,
            IOptions<LocalVideoProviderOptions> options)
        {
            this.encoderService = encoderService;
            this.options = options.Value;
        }

        // Properties.
        public string SourceName => options.JsonMetadataFilePath;

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
            var jsonMetadataFileDirectory = Path.GetDirectoryName(Path.GetFullPath(options.JsonMetadataFilePath))!;
            var localVideosMetadataDto = JsonSerializer.Deserialize<List<LocalVideoMetadataDto>>(
                await File.ReadAllTextAsync(options.JsonMetadataFilePath)) 
                ?? throw new InvalidDataException($"LocalFile wrong format in {options.JsonMetadataFilePath}");

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
                        var absoluteThumbnailFilePath = Path.IsPathFullyQualified(metadataDto.ThumbnailFilePath) ?
                            metadataDto.ThumbnailFilePath :
                            Path.Combine(jsonMetadataFileDirectory, metadataDto.ThumbnailFilePath);

                        using var thumbFileStream = File.OpenRead(absoluteThumbnailFilePath);
                        using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                        using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                        thumbnail = new ThumbnailLocalFile(absoluteThumbnailFilePath, thumbBitmap.ByteCount, thumbBitmap.Height, thumbBitmap.Width);
                    }

                    // Get video info.
                    var absoluteVideoFilePath = Path.IsPathFullyQualified(metadataDto.VideoFilePath) ?
                        metadataDto.VideoFilePath :
                        Path.Combine(jsonMetadataFileDirectory, metadataDto.VideoFilePath);

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
                            new VideoLocalFile(
                                absoluteVideoFilePath,
                                ffProbeResult.Streams.First().Height,
                                ffProbeResult.Streams.First().Width,
                                new FileInfo(absoluteVideoFilePath).Length)));

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

        public Task ReportEthernaReferencesAsync(string sourceVideoId, string ethernaIndexId, string ethernaPermalinkHash) =>
            Task.CompletedTask;

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
