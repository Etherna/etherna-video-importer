using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Models.Domain;
using Etherna.VideoImporter.Models.LocalVideoDto;
using Etherna.VideoImporter.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Services
{
    internal sealed class JsonListVideoProvider : IVideoProvider
    {
        // Fields.
        private readonly IEncoderService encoderService;
        private readonly IFFmpegService ffMpegService;
        private readonly JsonListVideoProviderOptions options;

        // Constructor.
        public JsonListVideoProvider(
            IEncoderService encoderService,
            IFFmpegService ffMpegService,
            IOptions<JsonListVideoProviderOptions> options)
        {
            this.encoderService = encoderService;
            this.ffMpegService = ffMpegService;
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
            string jsonData = await new SourceFile(options.JsonMetadataUri).ReadAsStringAsync();
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
                    // Build thumbnail.
                    var thumbnail = string.IsNullOrWhiteSpace(metadataDto.ThumbnailFilePath) ? null :
                        await ThumbnailSourceFile.BuildNewAsync(new SourceUri(
                            metadataDto.ThumbnailFilePath,
                            defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri));

                    // Build video.
                    videosMetadataDictionary.Add(
                        metadataDto.Id,
                        new LocalVideoMetadata(
                            metadataDto.Id,
                            metadataDto.Title,
                            metadataDto.Description,
                            thumbnail,
                            VideoSourceFile.BuildNew(
                                new SourceUri(metadataDto.VideoFilePath, defaultBaseDirectory: jsonMetadataDirectoryAbsoluteUri),
                                ffMpegService)));

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
    }
}
