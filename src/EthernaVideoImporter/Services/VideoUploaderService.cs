using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.EthernaVideoImporter.Models;
using EthernaVideoImporter.Models;
using Microsoft.AspNetCore.Components.Forms;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    internal class VideoUploaderService
    {
        // Privates.
        private readonly BeeNodeClient beeNodeClient;
        private readonly HttpClient httpClient;
        private readonly string gatewayUrl;
        private readonly string indexUrl;
        private readonly bool offerVideo;
        private readonly string userEthAddr;


        // Const.
        private const int BATCH_DEEP = 20;
        private const int BATCH_DURANTION_TIME = 31536000;
        private const int BATCH_WAITING_TIME = 30 * 1000;
        private const int BATCH_TIMEOUT_TIME = 5 * 60 * 1000;
        private const int BLOCK_TIME = 5;
        private const string INDEX_API_CREATEBATCH = "api/v0.3/videos";
        private const string EMBED_LINK_RESOURCE = "<iframe src=\"https://etherna.io/embed/{0}\" allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe>";
        private const string GATEWAY_API_CREATEBATCH = "api/v0.3/users/current/batches";
        private const string GATEWAY_API_CHAINSTATE = "api/v0.3/system/chainstate";
        private const string GATEWAY_API_GETBATCH = "api/v0.3/users/current/batches";
        private const string GATEWAY_API_GETBATCH_REFERENCE = "api/v0.3/System/postageBatchRef/";
        private const string GATEWAY_API_OFFER_RESOURCE = "api/v0.3/Resources/{0}/offers";
        static readonly JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Constractor.
        public VideoUploaderService(
            HttpClient httpClient,
            BeeNodeClient beeNodeClient,
            string gatewayUrl,
            string indexUrl,
            string userEthAddr,
            bool offerVideo)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (string.IsNullOrWhiteSpace(indexUrl))
                throw new ArgumentNullException(nameof(indexUrl));

            this.beeNodeClient = beeNodeClient;
            this.httpClient = httpClient;
            this.gatewayUrl = gatewayUrl;
            this.indexUrl = indexUrl;
            this.userEthAddr = userEthAddr;
            this.offerVideo = offerVideo;
        }

        // Public methods.
        public async Task StartAsync(
            VideoInfoWithData videoInfoWithData,
            bool pinVideo)
        {
            if (videoInfoWithData.ImportStatus == ImportStatus.Processed)
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
                Console.WriteLine("Create batch...");
                // Create batch.
                videoInfoWithData.BatchReferenceId = await CreateBatchIdFromReferenceAsync().ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.BatchCreated;
            }

            // Check and wait until created batchId is avaiable.
            Console.WriteLine("Waiting for batch ready...");
            int timeWaited = 0;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME)
                {
                    var ex = new InvalidOperationException("Batch not avaiable");
                    ex.Data.Add("BatchReferenceId", videoInfoWithData.BatchReferenceId);
                    throw ex;
                }

                // Waiting for batchId avaiable.
                await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
                videoInfoWithData.BatchId = await GetBatchIdFromReference(videoInfoWithData.BatchReferenceId!).ConfigureAwait(false);
                timeWaited += BATCH_WAITING_TIME;
            } while (string.IsNullOrWhiteSpace(videoInfoWithData.BatchId));

            // Check and wait until created batch is usable.
            timeWaited = 0;
            BatchMinimalInfoDto? batch;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME)
                {
                    var ex = new InvalidOperationException("Batch not usable");
                    ex.Data.Add("BatchId", videoInfoWithData.BatchId);
                    throw ex;
                }

                // Waiting for batch ready.
                await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
                batch = await GetBatchAsync(videoInfoWithData.BatchId).ConfigureAwait(false);
                timeWaited += BATCH_WAITING_TIME;
            } while (batch is null ||
                    !batch.Usable);

            // Upload file.
            if (string.IsNullOrWhiteSpace(videoInfoWithData.VideoReference))
            {
                Console.WriteLine("Uploading video in progress...");
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(videoInfoWithData.DownloadedFilePath!),
                    Path.GetFileName(videoInfoWithData.DownloadedFilePath!),
                    MimeTypes.GetMimeType(Path.GetFileName(videoInfoWithData.DownloadedFilePath!)));

                videoInfoWithData.VideoReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    videoInfoWithData.BatchId!,
                    files: new List<FileParameterInput> { fileParameterInput },
                    swarmPin: pinVideo).ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.VideoUploaded;
            }

            // Upload thumbnail.
            if (string.IsNullOrWhiteSpace(videoInfoWithData.ThumbnailReference) &&
                File.Exists(videoInfoWithData.DownloadedThumbnailPath))
            {
                Console.WriteLine("Uploading thumbnail in progress...");
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(videoInfoWithData.DownloadedThumbnailPath!),
                    Path.GetFileName(videoInfoWithData.DownloadedThumbnailPath!),
                    MimeTypes.GetMimeType(Path.GetFileName(videoInfoWithData.DownloadedThumbnailPath!)));

                videoInfoWithData.ThumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    videoInfoWithData.BatchId!,
                    files: new List<FileParameterInput> { fileParameterInput },
                    swarmPin: pinVideo).ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.ThumbnailUploaded;
            }

            // Upload metadata.
            if (string.IsNullOrWhiteSpace(videoInfoWithData.HashMetadataReference))
            {
                Console.WriteLine("Uploading metadata in progress...");
                videoInfoWithData.HashMetadataReference = await UploadMetadataAsync(
                    videoInfoWithData.VideoReference!,
                    videoInfoWithData.BatchId!,
                    videoInfoWithData,
                    pinVideo).ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.MetadataUploaded;
            }

            if (offerVideo)
            {
                Console.WriteLine("Flag video by offer from creator in progress...");
                await OfferResourceAsync(videoInfoWithData.VideoReference).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(videoInfoWithData.ThumbnailReference))
                    await OfferResourceAsync(videoInfoWithData.ThumbnailReference).ConfigureAwait(false);
                await OfferResourceAsync(videoInfoWithData.HashMetadataReference).ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.ReferenceOffer;
            }

            // Sync Index.
            if (string.IsNullOrWhiteSpace(videoInfoWithData.IndexVideoId))
            {
                Console.WriteLine("Video indexing in progress...");
                videoInfoWithData.IndexVideoId = await IndexAsync(videoInfoWithData.HashMetadataReference!).ConfigureAwait(false);
                videoInfoWithData.ImportStatus = ImportStatus.IndexSynced;
            }

            // Embed link.
            videoInfoWithData.EmbedLink = string.Format(CultureInfo.InvariantCulture, EMBED_LINK_RESOURCE, videoInfoWithData.HashMetadataReference);

            // Remove downloaded files.
            if (File.Exists(videoInfoWithData.DownloadedFilePath))
                File.Delete(videoInfoWithData.DownloadedFilePath);
            if (File.Exists(videoInfoWithData.DownloadedThumbnailPath))
                File.Delete(videoInfoWithData.DownloadedThumbnailPath);

            videoInfoWithData.ImportStatus = ImportStatus.Processed;
            videoInfoWithData.VideoStatusNote = "";
            return;
        }

        // Private methods.
        private async Task<string> CreateBatchIdFromReferenceAsync()
        {
            var httpResponse = await httpClient.GetAsync(new Uri(gatewayUrl + GATEWAY_API_CHAINSTATE)).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var chainPriceDto = JsonSerializer.Deserialize<ChainPriceDto>(responseText, options);
            if (chainPriceDto is null)
                throw new ArgumentNullException("Chainstate result is null");

            var amount = (long)BATCH_DURANTION_TIME * BLOCK_TIME / chainPriceDto.CurrentPrice;
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl + GATEWAY_API_CREATEBATCH + $"?depth={BATCH_DEEP}&amount={amount}"), httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<BatchMinimalInfoDto?> GetBatchAsync(string batchId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{gatewayUrl}{GATEWAY_API_GETBATCH}/{batchId}")).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                return null;

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<BatchMinimalInfoDto>(responseText, options);
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
            var indexManifestRequest = new IndexManifestRequest(hashReferenceMetadata);
            using var httpContent = new StringContent(JsonSerializer.Serialize(indexManifestRequest), Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(new Uri(indexUrl + INDEX_API_CREATEBATCH), httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<bool?> OfferResourceAsync(string reference)
        {
            var urlOffer = string.Format(CultureInfo.InvariantCulture, GATEWAY_API_OFFER_RESOURCE, reference);
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl + urlOffer), httpContent).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                throw new InvalidProgramException($"Error during offer resource");

            return true;
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

            SwarmImageRaw? swarmImageRaw = null;
            if (!string.IsNullOrWhiteSpace(videoDataInfoDto.ThumbnailReference) &&
                !string.IsNullOrWhiteSpace(videoDataInfoDto.DownloadedThumbnailPath))
            {
                using var input = File.OpenRead(videoDataInfoDto.DownloadedThumbnailPath);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);
                var hash = Blurhash.SkiaSharp.Blurhasher.Encode(sourceImage, 4, 4);
                //var sourceData = Blurhasher.
                swarmImageRaw = new SwarmImageRaw(
                    sourceImage.Width / sourceImage.Height,
                    "",
                    new Dictionary<string, string> { { $"{sourceImage.Width}w", videoDataInfoDto.ThumbnailReference } },
                    "1.0");
            }

            var metadataVideoDto = new MetadataVideoDto(
                postageBatch,
                videoDataInfoDto.Description,
                videoDataInfoDto.Duration,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                videoDataInfoDto.Quality,
                userEthAddr,
                new List<MetadataVideoSource> { new MetadataVideoSource(videoDataInfoDto.Bitrate, videoDataInfoDto.Quality, referenceVideo, videoDataInfoDto.Size) },
                swarmImageRaw,
                videoDataInfoDto.Title,
                null,
                "1.1");

            var tmpMetadata = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, metadataVideoDto.ToJson()).ConfigureAwait(false);

                // Upload file.
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(tmpMetadata),
                    Path.GetFileName("metadata.json"),
                    MimeTypes.GetMimeType("application/json"));

                using var fileStream = File.OpenRead(tmpMetadata);
                return await beeNodeClient.GatewayClient!.UploadFileAsync(
                    postageBatch,
                    files: new List<FileParameterInput> { fileParameterInput },
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
