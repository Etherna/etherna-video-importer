using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Dtos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    internal class VideoUploaderService
    {
        private readonly BeeNodeClient beeNodeClient;
        private readonly string indexUrl;

        // Constractor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            string indexUrl)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (string.IsNullOrWhiteSpace(indexUrl))
                throw new ArgumentNullException(nameof(indexUrl));

            this.beeNodeClient = beeNodeClient;
            this.indexUrl = indexUrl;
        }

        // Public methods.
        public async Task Start(VideoDataInfoDto videoDataInfoDto)
        {
            if (videoDataInfoDto.VideoStatus == VideoStatus.Processed)
                return;
            if (videoDataInfoDto.VideoStatus == VideoStatus.NotProcess ||
                videoDataInfoDto.VideoStatus == VideoStatus.Downloading)
                throw new InvalidOperationException($"Invalid Status: {videoDataInfoDto.VideoStatus}");

            var postageBatch = videoDataInfoDto.BatchId;
            if (videoDataInfoDto.VideoStatus == VideoStatus.Downloaded)
            {
                // Create batch.
                postageBatch = await beeNodeClient.DebugClient!.BuyPostageBatchAsync(1, 28);
                videoDataInfoDto.BatchId = postageBatch;
                videoDataInfoDto.VideoStatus = VideoStatus.VideoUploading;
                await Task.Delay(90000);
            }

            var referenceVideo = videoDataInfoDto.VideoReference;
            if (videoDataInfoDto.VideoStatus == VideoStatus.VideoUploading)
            {
                // Upload file.
                var fileParameterInput = new FileParameterInput(
                File.OpenRead(videoDataInfoDto.DownloadedFilePath!),
                Path.GetFileName(videoDataInfoDto.DownloadedFilePath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoDataInfoDto.DownloadedFilePath!)));
                referenceVideo = await beeNodeClient.GatewayClient!.UploadFileAsync(postageBatch!, files: new List<FileParameterInput> { fileParameterInput }, swarmCollection: false);
                videoDataInfoDto.VideoReference = referenceVideo;
                videoDataInfoDto.VideoStatus = VideoStatus.VideoUploaded;
            }

            var referenceMetadata = videoDataInfoDto.MetadataReference;
            if (videoDataInfoDto.VideoStatus == VideoStatus.VideoUploaded)
            {
                // Upload metadata.
                referenceMetadata = await UploadMetadataAsync(referenceVideo!, postageBatch!, videoDataInfoDto);
                videoDataInfoDto.MetadataReference = referenceMetadata;
                videoDataInfoDto.VideoStatus = VideoStatus.MetadataUploaded;
            }

            if (videoDataInfoDto.VideoStatus == VideoStatus.MetadataUploaded)
            {
                // Sync Index.
                var videoId = await IndexAsync(referenceMetadata!);
                videoDataInfoDto.IndexVideoId = videoId;
                videoDataInfoDto.VideoStatus = VideoStatus.IndexSynced;
            }

            // Remove downloaded file.
            if (File.Exists(videoDataInfoDto.DownloadedFilePath))
                File.Delete(videoDataInfoDto.DownloadedFilePath);

            videoDataInfoDto.VideoStatus = VideoStatus.Processed;
            return;
        }

        // Private methods.
        private async Task<string> IndexAsync(string referenceMetadata)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(indexUrl);
            using var httpContent = new StringContent(JsonConvert.SerializeObject(referenceMetadata), Encoding.UTF8, "application/json");

#pragma warning disable CA2234
            var httpResponse = await client.PostAsync("/api/Videos", httpContent);
#pragma warning restore CA2234

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync();
        }

        private async Task<string> UploadMetadataAsync(
            string referenceVideo,
            string postageBatch,
            VideoDataInfoDto videoDataInfoDto)
        {
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.Description))
                throw new InvalidOperationException("Description not defined");
            if (string.IsNullOrWhiteSpace(videoDataInfoDto.Quality))
                throw new InvalidOperationException("Quality not defined");

            var metadataVideoDto = new MetadataVideoDto(
                postageBatch,
                videoDataInfoDto.Description!,
                videoDataInfoDto.Duration,
                -1L, //Created at
                videoDataInfoDto.Quality!,
                "owner",
                new List<MetadataVideoSource> { new MetadataVideoSource(videoDataInfoDto.Bitrate, videoDataInfoDto.Quality!, referenceVideo, videoDataInfoDto.Size) },
                null,
                videoDataInfoDto.Title!,
                -1); //Updated at

            var tmpMetadata = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, metadataVideoDto.ToJson());

                // Upload file.
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(tmpMetadata),
                    Path.GetFileName("metadata.json"),
                    MimeTypes.GetMimeType("application/json"));

                return await beeNodeClient.GatewayClient!.UploadFileAsync(
                    postageBatch, 
                    files: new List<FileParameterInput> { fileParameterInput }, 
                    swarmCollection: false);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }
        }
    }
}
