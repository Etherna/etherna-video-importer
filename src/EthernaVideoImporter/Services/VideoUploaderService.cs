using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.EthernaVideoImporter.Dtos;
using EthernaVideoImporter.Dtos;
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
        public async Task StartAsync(
            VideoInfoWithData videoInfoWithData,
            bool pinVideo)
        {
            if (videoInfoWithData.VideoStatus == VideoStatus.Processed)
                return;
            if (videoInfoWithData.VideoStatus == VideoStatus.NotProcess)
                throw new InvalidOperationException($"Invalid Status: {videoInfoWithData.VideoStatus}");

            if (string.IsNullOrWhiteSpace(videoInfoWithData.BatchId))
            {
                // Create batch.
                videoInfoWithData.BatchId = await beeNodeClient.DebugClient!.BuyPostageBatchAsync(1, 28).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.BatchCreated;
                await Task.Delay(90000).ConfigureAwait(false);

                //TODO check avaiable batch or another await with while
            }
            else
            {
                //TODO check validity
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.VideoReference))
            {
                // Upload file.
                var fileParameterInput = new FileParameterInput(
                File.OpenRead(videoInfoWithData.DownloadedFilePath!),
                Path.GetFileName(videoInfoWithData.DownloadedFilePath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoInfoWithData.DownloadedFilePath!)));
                videoInfoWithData.VideoReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    videoInfoWithData.BatchId!, 
                    files: new List<FileParameterInput> { fileParameterInput }, 
                    swarmCollection: false,
                    swarmPin: pinVideo).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.VideoUploaded;
            }
            else
            {
                //TODO check validity
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.HashMetadataReference))
            {
                // Upload metadata.
                videoInfoWithData.HashMetadataReference = await UploadMetadataAsync(
                    videoInfoWithData.VideoReference!, 
                    videoInfoWithData.BatchId!, 
                    videoInfoWithData).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.MetadataUploaded;
            }
            else
            {
                //TODO check validity
            }

            if (string.IsNullOrWhiteSpace(videoInfoWithData.IndexVideoId))
            {
                // Sync Index.
                videoInfoWithData.IndexVideoId = await IndexAsync(videoInfoWithData.HashMetadataReference!).ConfigureAwait(false);
                videoInfoWithData.VideoStatus = VideoStatus.IndexSynced;
            }
            else
            {
                //TODO check validity
            }

            // Remove downloaded file.
            if (File.Exists(videoInfoWithData.DownloadedFilePath))
                File.Delete(videoInfoWithData.DownloadedFilePath);

            videoInfoWithData.VideoStatus = VideoStatus.Processed;
            videoInfoWithData.VideoStatusNote = "";
            return;
        }

        // Private methods.
        private async Task<string> IndexAsync(string hashReferenceMetadata)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(indexUrl);
            using var httpContent = new StringContent(hashReferenceMetadata, Encoding.UTF8, "application/json");

#pragma warning disable CA2234
            var httpResponse = await client.PostAsync("/api/Videos", httpContent).ConfigureAwait(false);
#pragma warning restore CA2234

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> UploadMetadataAsync(
            string referenceVideo,
            string postageBatch,
            VideoInfoWithData videoDataInfoDto)
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
