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
        private readonly string gatewayUrl;
        private readonly string indexUrl;

        // Constractor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            string gatewayUrl,
            string indexUrl)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (string.IsNullOrWhiteSpace(indexUrl))
                throw new ArgumentNullException(nameof(indexUrl));

            this.beeNodeClient = beeNodeClient;
            this.gatewayUrl = gatewayUrl;
            this.indexUrl = indexUrl;
        }

        // Public methods.
        public async Task StartAsync(VideoDataInfo videoDataInfoDto)
        {
            if (videoDataInfoDto.VideoStatus == VideoStatus.Processed)
                return;
            if (videoDataInfoDto.VideoStatus == VideoStatus.NotProcess)
                throw new InvalidOperationException($"Invalid Status: {videoDataInfoDto.VideoStatus}");

            if (videoDataInfoDto.VideoStatus == VideoStatus.Downloaded)
            {
                // Create batch.
                videoDataInfoDto.BatchId = await beeNodeClient.DebugClient!.BuyPostageBatchAsync(1, 28).ConfigureAwait(false);
                videoDataInfoDto.VideoStatus = VideoStatus.BatchCreated;
                await Task.Delay(90000).ConfigureAwait(false);
            }
            
            if (videoDataInfoDto.VideoStatus == VideoStatus.BatchCreated)
            {
                // Upload file.
                var fileParameterInput = new FileParameterInput(
                File.OpenRead(videoDataInfoDto.DownloadedFilePath!),
                Path.GetFileName(videoDataInfoDto.DownloadedFilePath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoDataInfoDto.DownloadedFilePath!)));
                videoDataInfoDto.VideoReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    videoDataInfoDto.BatchId!, 
                    files: new List<FileParameterInput> { fileParameterInput }, 
                    swarmCollection: false).ConfigureAwait(false);
                videoDataInfoDto.VideoStatus = VideoStatus.VideoUploaded;
            }

            if (videoDataInfoDto.VideoStatus == VideoStatus.VideoUploaded)
            {
                // Upload metadata.
                videoDataInfoDto.MetadataReference = await UploadMetadataAsync(
                    videoDataInfoDto.VideoReference!, 
                    videoDataInfoDto.BatchId!, 
                    videoDataInfoDto).ConfigureAwait(false);
                videoDataInfoDto.VideoStatus = VideoStatus.MetadataUploaded;
            }

            if (videoDataInfoDto.VideoStatus == VideoStatus.MetadataUploaded)
            {
                // Sync Index.
                videoDataInfoDto.IndexVideoId = await IndexAsync(videoDataInfoDto.MetadataReference!).ConfigureAwait(false);
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
            var httpResponse = await client.PostAsync("/api/Videos", httpContent).ConfigureAwait(false);
#pragma warning restore CA2234

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> UploadMetadataAsync(
            string referenceVideo,
            string postageBatch,
            VideoDataInfo videoDataInfoDto)
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
                await File.WriteAllTextAsync(tmpMetadata, metadataVideoDto.ToJson()).ConfigureAwait(false);

                // Upload file.
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(tmpMetadata),
                    Path.GetFileName("metadata.json"),
                    MimeTypes.GetMimeType("application/json"));

                return await beeNodeClient.GatewayClient!.UploadFileAsync(
                    postageBatch, 
                    files: new List<FileParameterInput> { fileParameterInput }, 
                    swarmCollection: false).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }
        }
    }
}
