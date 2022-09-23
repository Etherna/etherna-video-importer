using Etherna.BeeNet;
using Etherna.BeeNet.DtoModels;
using Etherna.EthernaVideoImporter.Models;
using EthernaVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    internal class VideoUploaderService
    {
        private readonly BeeNodeClient beeNodeClient;
        private readonly string gatewayUrl;
        private readonly string indexUrl;
        private const int BATCH_WAITING_TIME = 30 * 1000;
        private const int BATCH_TIMEOUT_TIME = 5 * 60 * 1000;
        private const string GATEWAY_API_CREATEBATCH = "/api/v0.3/users/current/batches";
        private const string GATEWAY_API_GETBATCH = "/api/v0.3/users/current/batches";

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
        public async Task StartAsync(
            VideoInfoWithData videoInfoWithData,
            bool pinVideo)
        {
            if (videoInfoWithData.VideoStatus == VideoStatus.Processed)
                return;
            if (string.IsNullOrWhiteSpace(videoInfoWithData.DownloadedFilePath) ||
                !File.Exists(videoInfoWithData.DownloadedFilePath))
            {
                var ex = new InvalidOperationException($"Video to upload not found");
                ex.Data.Add("DownloadedFilePath", videoInfoWithData.DownloadedFilePath);
                videoInfoWithData.DownloadedFilePath = "";
                throw ex;
            }

            // Create new batch.
            if (string.IsNullOrWhiteSpace(videoInfoWithData.BatchId))
            {
                // Create batch.
                videoInfoWithData.BatchId = await CreateBatchAsync().ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.BatchCreated;
                await Task.Delay(90000).ConfigureAwait(false);
            }

            // Check and wait until created batch is ready.
            var batch = await GetBatchAsync(videoInfoWithData.BatchId).ConfigureAwait(false);
            int timeWaited = 0;
            while (batch is null ||
                    !batch.Exists ||
                    !batch.Usable)
            {
                // Timeout throw exception and reset the BatchId field.
                if (timeWaited >= BATCH_TIMEOUT_TIME)
                {
                    var ex = new InvalidOperationException("Batch not avaiable");
                    ex.Data.Add("BatchId", videoInfoWithData.BatchId);
                    videoInfoWithData.BatchId = "";
                    throw ex;
                }

                // Waiting for batch ready.
                await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
                batch = await GetBatchAsync(videoInfoWithData.BatchId).ConfigureAwait(false);
                timeWaited += BATCH_WAITING_TIME;
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.VideoReference))
            {
                // Upload file.
                using var fileStream = File.OpenRead(videoInfoWithData.DownloadedFilePath!);
                videoInfoWithData.VideoReference = await beeNodeClient.GatewayClient!.UploadDataAsync(
                    videoInfoWithData.BatchId!,
                    body: fileStream,
                    swarmPin: pinVideo).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.VideoUploaded;
            }
            else
            {
                //TODO check if exist, is possible to check file without download all?
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.HashMetadataReference))
            {
                // Upload metadata.
                videoInfoWithData.HashMetadataReference = await UploadMetadataAsync(
                    videoInfoWithData.VideoReference!,
                    videoInfoWithData.BatchId!,
                    videoInfoWithData,
                    pinVideo).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.MetadataUploaded;
            }
            else
            {
                //TODO check if exist, is possible to check file without download all?
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.IndexVideoId))
            {
                // Sync Index.
                videoInfoWithData.IndexVideoId = await IndexAsync(videoInfoWithData.HashMetadataReference!).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.IndexSynced;
            }
            else
            {
                //TODO check if exist
            }

            // Remove downloaded file.
            if (File.Exists(videoInfoWithData.DownloadedFilePath))
                File.Delete(videoInfoWithData.DownloadedFilePath);

            videoInfoWithData.VideoStatus = VideoStatus.Processed;
            videoInfoWithData.VideoStatusNote = "";
            return;
        }

        // Private methods.
        private async Task<string> CreateBatchAsync()
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(gatewayUrl);
            using var httpContent = new StringContent("{depth: 20, amount: 1}", Encoding.UTF8, "application/json"); //TODO insert value for batch in body
            var httpResponse = await client.PostAsync(GATEWAY_API_CREATEBATCH, httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<PostageBatchDto?> GetBatchAsync(string batchId)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(gatewayUrl);

            var httpResponse = await client.GetAsync($"{GATEWAY_API_GETBATCH}/{batchId}").ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                return null;

            using var responseStream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<PostageBatchDto>(responseStream).ConfigureAwait(false);
        }

        private async Task<string> IndexAsync(string hashReferenceMetadata)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(indexUrl);
            using var httpContent = new StringContent(hashReferenceMetadata, Encoding.UTF8, "application/json");

            var httpResponse = await client.PostAsync("/api/Videos", httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> UploadMetadataAsync(
            string referenceVideo,
            string postageBatch,
            VideoInfoWithData videoDataInfoDto,
            bool swarmPin)
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
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                videoDataInfoDto.Quality!,
                "owner", //TODO take from SSO
                new List<MetadataVideoSource> { new MetadataVideoSource(videoDataInfoDto.Bitrate, videoDataInfoDto.Quality!, referenceVideo, videoDataInfoDto.Size) },
                null,
                videoDataInfoDto.Title!,
                null);

            var tmpMetadata = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, metadataVideoDto.ToJson()).ConfigureAwait(false);

                // Upload file.
                using var fileStream = File.OpenRead(tmpMetadata);
                return await beeNodeClient.GatewayClient!.UploadDataAsync(
                    postageBatch,
                    swarmPin: swarmPin).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }
        }
    }
}
