using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.LocalVideoDto;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    public sealed class LocalVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly string localFile;
        private readonly IEncoderService encoderService;
        private readonly bool includeAudioTrack;
        private readonly IEnumerable<int> supportedHeightResolutions;

        // Constructor.
        public LocalVideoProvider(
            string localFile,
            IEncoderService encoderService,
            bool includeAudioTrack,
            IEnumerable<int> supportedHeightResolutions)
        {
            this.localFile = localFile;
            this.encoderService = encoderService;
            this.includeAudioTrack = includeAudioTrack;
            this.supportedHeightResolutions = supportedHeightResolutions;
        }

        // Properties.
        public string SourceName => localFile;

        // Methods.
        public async Task<Video> GetVideoAsync(
            VideoMetadataBase videoMetadata,
            DirectoryInfo tempDirectory)
        {
            if (tempDirectory is null)
                throw new ArgumentNullException(nameof(tempDirectory));

            var localVideoMetadata = videoMetadata as LocalVideoMetadata ?? throw new ArgumentException($"Metadata bust be of type {nameof(LocalVideoMetadata)}", nameof(videoMetadata));

            // Video info.
            var videoHeight = 0;
            var videoWidth = 0;
            var byteSizeVideo = 0;
            var videoLocalFile = new VideoLocalFile(localVideoMetadata.FilePath, videoHeight, videoWidth, byteSizeVideo);

            // Transcode video resolutions.
            var encodedFiles = await encoderService.EncodeVideosAsync(videoLocalFile, tempDirectory, supportedHeightResolutions, includeAudioTrack);

            // Get thumbnail.
            List<ThumbnailLocalFile> thumbnailFiles = new();
            if (localVideoMetadata.Thumbnail is not null)
            {
                thumbnailFiles = await DownscaleThumbnailAsync(localVideoMetadata.Thumbnail, tempDirectory);
            }
            return new Video(videoMetadata, encodedFiles, thumbnailFiles);
        }

        public async Task<IEnumerable<VideoMetadataBase>> GetVideosMetadataAsync()
        {
            var localVideos = JsonSerializer.Deserialize<List<ArchiveLocalVideoDto>>(localFile) ?? throw new InvalidDataException($"LocalFile wrong format in {localFile}");

            var videosMetadata = new List<VideoMetadataBase>();
            foreach (var video in localVideos)
            {
                try
                {
                    // Thumbnail info.
                    ThumbnailLocalFile? thumbnail = null;
                    if (!string.IsNullOrWhiteSpace(video.Thumbnail))
                    {
                        using var thumbFileStream = File.OpenRead(video.Thumbnail);
                        using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                        using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                        thumbnail = new ThumbnailLocalFile(video.Thumbnail, thumbBitmap.ByteCount, thumbBitmap.Height, thumbBitmap.Width);
                        await thumbFileStream.DisposeAsync();
                    }

                    // Video info.
                    var duration = new TimeSpan(1);
                    var videoQualityLabel = "1080p";
                    // quando audio è null controllare che il video contenga anche l'audio

                    videosMetadata.Add(new LocalVideoMetadata(
                        video.Description,
                        duration,
                        videoQualityLabel,
                        thumbnail,
                        video.Title,
                        video.Video));

                    Console.WriteLine($"Downloaded metadata for {video.Title}");
                }
                catch (HttpRequestException ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error retrieving video: {video.Title}.");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"Found {videosMetadata.Count} videos");

            return videosMetadata;
        }

        // Helpers.
        private async Task<List<ThumbnailLocalFile>> DownscaleThumbnailAsync(
                ThumbnailLocalFile betsResolutionThumbnail,
                DirectoryInfo importerTempDirectoryInfo)
        {
            List<ThumbnailLocalFile> thumbnails = new();

            using var thumbFileStream = File.OpenRead(betsResolutionThumbnail.FilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailLocalFile.ThumbnailResponsiveSizes)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / betsResolutionThumbnail.AspectRatio);

                using SKBitmap scaledBitmap = thumbBitmap.Resize(new SKImageInfo(responsiveWidthSize, responsiveHeightSize), SKFilterQuality.Medium);
                using SKImage scaledImage = SKImage.FromBitmap(scaledBitmap);
                using SKData data = scaledImage.Encode();

                var thumbnailResizedPath = Path.Combine(importerTempDirectoryInfo.FullName, $"thumb_{responsiveWidthSize}_{responsiveHeightSize}_{Guid.NewGuid()}.jpg");
                using FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew);
                await data.AsStream().CopyToAsync(outputFileStream);

                thumbnails.Add(new ThumbnailLocalFile(
                    thumbnailResizedPath,
                    new FileInfo(thumbnailResizedPath).Length,
                    responsiveHeightSize,
                    responsiveWidthSize));
            }

            return thumbnails;
        }
    }
}
