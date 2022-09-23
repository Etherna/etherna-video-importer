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
        private readonly HttpClient httpClient;
        private readonly string gatewayUrl;
        private readonly string indexUrl;
        private const int BATCH_DEEP = 20;
        private const int BATCH_DURANTION_TIME = 31536000;
        private const int BATCH_WAITING_TIME = 30 * 1000;
        private const int BATCH_TIMEOUT_TIME = 5 * 60 * 1000;
        private const int BLOCK_TIME = 5;
        private const string INDEX_API_CREATEBATCH = "api/v0.3/videos";
        private const string GATEWAY_API_CREATEBATCH = "api/v0.3/users/current/batches";
        private const string GATEWAY_API_CHAINSTATE = "api/v0.3/system/chainstate";
        private const string GATEWAY_API_GETBATCH_REFERENCE = "api/v0.3/System/postageBatchRef/";
        //private const string GATEWAY_API_GETBATCH = "api/v0.3/users/current/batches";
        

        static readonly JsonSerializerOptions serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Constractor.
        public VideoUploaderService(
            HttpClient httpClient,
            BeeNodeClient beeNodeClient,
            string gatewayUrl,
            string indexUrl)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (string.IsNullOrWhiteSpace(indexUrl))
                throw new ArgumentNullException(nameof(indexUrl));

            this.beeNodeClient = beeNodeClient;
            this.httpClient = httpClient;
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
            if (string.IsNullOrWhiteSpace(videoInfoWithData.BatchReferenceId))
            {
                // Create batch.
                videoInfoWithData.BatchReferenceId = await CreateBatchAsync().ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.BatchCreated;
                await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
            }

            // Check and wait until created batch is ready.
            videoInfoWithData.BatchId = await GetBatchIdFromReference(videoInfoWithData.BatchReferenceId!).ConfigureAwait(false);
            int timeWaited = 0;
            while (string.IsNullOrWhiteSpace(videoInfoWithData.BatchId))
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
                videoInfoWithData.BatchId = await GetBatchIdFromReference(videoInfoWithData.BatchReferenceId!).ConfigureAwait(false);
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

            if (string.IsNullOrWhiteSpace(videoInfoWithData.IndexVideoId))
            {
                // Sync Index.
                videoInfoWithData.IndexVideoId = await IndexAsync(videoInfoWithData.HashMetadataReference!).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.IndexSynced;
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
            var httpResponse = await httpClient.GetAsync(new Uri(gatewayUrl + GATEWAY_API_CHAINSTATE)).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var chainPriceDto = JsonSerializer.Deserialize<ChainPriceDto>(responseText, serializeOptions);
            if (chainPriceDto is null)
                throw new ArgumentNullException("Chainstate result is null");

            var amount = (long)BATCH_DURANTION_TIME * BLOCK_TIME / chainPriceDto.CurrentPrice;
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl+GATEWAY_API_CREATEBATCH + $"?depth={BATCH_DEEP}&amount={amount}"), httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> GetBatchIdFromReference(string referenceId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{gatewayUrl}{GATEWAY_API_GETBATCH_REFERENCE}/{referenceId}")).ConfigureAwait(false);

            if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                return "";

            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> IndexAsync(string hashReferenceMetadata)
        {
            using var httpContent = new StringContent($"{{ manifestHash: {hashReferenceMetadata}}}", Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(new Uri(indexUrl + INDEX_API_CREATEBATCH), httpContent).ConfigureAwait(false);

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
                    body: fileStream,
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
