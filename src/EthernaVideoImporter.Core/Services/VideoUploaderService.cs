//   Copyright 2022-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using Etherna.VideoImporter.Core.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class VideoUploaderService : IVideoUploaderService
    {
        // Fields.
        private readonly BeeNodeClient beeNodeClient;
        private readonly EthernaUserClientsAdapter ethernaClientService;
        private readonly HttpClient httpClient;
        private readonly bool includeAudioTrack;
        private readonly int ttlPostageStamp;
        private readonly string userEthAddr;


        // Const.
        private readonly TimeSpan BATCH_CHECK_TIME = new(0, 0, 0, 10);
        private readonly TimeSpan BATCH_TIMEOUT_TIME = new(0, 0, 7, 0);
        private const int MAX_RETRY = 3;

        // Constractor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            EthernaUserClientsAdapter ethernaClientService,
            string userEthAddr,
            HttpClient httpClient,
            bool includeAudioTrack,
            int ttlPostageStamp)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (ethernaClientService is null)
                throw new ArgumentNullException(nameof(ethernaClientService));

            this.beeNodeClient = beeNodeClient;
            this.ethernaClientService = ethernaClientService;
            this.userEthAddr = userEthAddr;
            this.httpClient = httpClient;
            this.includeAudioTrack = includeAudioTrack;
            this.ttlPostageStamp = ttlPostageStamp;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo)
        {
            if (videoData?.VideoDataResolutions is null ||
                videoData.VideoDataResolutions.Count <= 0)
                return;

            // Create new batch.
            Console.WriteLine("Create batch...");

            var batchReferenceId = await ethernaClientService.CreateBatchAsync(videoData, ttlPostageStamp).ConfigureAwait(false);

            // Check and wait until created batchId is avaiable.
            Console.WriteLine("Waiting for batch ready...");

            double timeWaited = 0;
            string batchId;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME.TotalSeconds)
                {
                    var ex = new InvalidOperationException("Batch not avaiable");
                    ex.Data.Add("BatchReferenceId", batchReferenceId);
                    throw ex;
                }

                // Waiting for batchId avaiable.
                await Task.Delay((int)BATCH_CHECK_TIME.TotalMilliseconds).ConfigureAwait(false);
                //batchId = await ethernaClientService.GetBatchIdFromBatchReferenceAsync(batchReferenceId).ConfigureAwait(false);
                batchId = await GetBatchIdFromReference(batchReferenceId).ConfigureAwait(false);
                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (string.IsNullOrWhiteSpace(batchId));

            // Check and wait until created batch is usable.
            timeWaited = 0;
            bool batchUsable;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME.TotalSeconds)
                {
                    var ex = new InvalidOperationException("Batch not usable");
                    ex.Data.Add("BatchId", batchId);
                    throw ex;
                }

                // Waiting for batch ready.
                await Task.Delay((int)BATCH_CHECK_TIME.TotalMilliseconds).ConfigureAwait(false);
                batchUsable = await ethernaClientService.IsBatchUsableAsync(batchId).ConfigureAwait(false);
                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (!batchUsable);

            // Upload video reoslutions.
            foreach (var specificVideoResolution in videoData.VideoDataResolutions)
            {
                // Upload video.
                var videoReference = await UploadFileVideoAsync(pinVideo, specificVideoResolution, batchId).ConfigureAwait(false);
                specificVideoResolution.SetUploadedVideoReference(videoReference);
                if (offerVideo)
                    await ethernaClientService.OfferResourceAsync(specificVideoResolution.UploadedVideoReference!).ConfigureAwait(false);

                await UploadThumbnailAsync(pinVideo, specificVideoResolution, batchId).ConfigureAwait(false);
                //if (offerVideo)
                await ethernaClientService.OfferResourceAsync(specificVideoResolution.UploadedThumbnailReference!).ConfigureAwait(false);
            }

            // Upload metadata.
            var hashMetadataReference = await UploadMetadataAsync(
                videoData,
                batchId,
                pinVideo).ConfigureAwait(false);
            //if (offerVideo)
            await ethernaClientService.OfferResourceAsync(hashMetadataReference).ConfigureAwait(false);

            // Sync Index.
            Console.WriteLine("Video indexing in progress...");
            await ethernaClientService.UpsertManifestToIndex(
                hashMetadataReference,
                videoData)
                .ConfigureAwait(false);
        }

        public async Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool pinVideo)
        {
            if (videoManifestDto is null)
                throw new ArgumentNullException(nameof(videoManifestDto));
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            var metadataManifestInsertInput = new MetadataManifestInsertInput(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userEthAddr,
                videoManifestDto.BatchId,
                videoData.Description,
                videoData.VideoDataResolutions.First().Duration,
                 $"720p",
                 JsonUtility.ToJson(new MetadataPersonalDataDto { Mode = MetadataUploadMode.DevconImporter, VideoId = videoData.YoutubeId! }),
                 new MetadataImageInput(
                     videoManifestDto.Thumbnail.AspectRatio,
                     videoManifestDto.Thumbnail.Blurhash,
                     videoManifestDto.Thumbnail.Sources),
                 videoData.Title,
                 videoManifestDto.Sources);

            return await UploadMetadataAsync(
                metadataManifestInsertInput,
                videoData,
                pinVideo).ConfigureAwait(false);
        }

        public async Task<string> UploadMetadataAsync(
            MetadataManifestInsertInput videoManifestDto,
            VideoData videoData,
            bool swarmPin)
        {
            if (videoManifestDto is null)
                throw new ArgumentNullException(nameof(videoManifestDto));
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            var tmpMetadata = Path.GetTempFileName();
            var hashMetadataReference = "";
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, JsonUtility.ToJson(videoManifestDto)).ConfigureAwait(false);

                var i = 0;
                while (i < MAX_RETRY &&
                    string.IsNullOrWhiteSpace(hashMetadataReference))
                    try
                    {
                        i++;
                        // Upload file.
                        var fileParameterInput = new FileParameterInput(
                            File.OpenRead(tmpMetadata),
                            Path.GetFileName("metadata.json"),
                            MimeTypes.GetMimeType("application/json"));
                        hashMetadataReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                            videoManifestDto.BatchId!,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: swarmPin).ConfigureAwait(false);
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                if (string.IsNullOrWhiteSpace(hashMetadataReference))
                    throw new InvalidOperationException("Some error during upload of metadata");

                videoData.SetEthernaPermalink(hashMetadataReference);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }

            return hashMetadataReference;
        }

        // Private methods.
        private async Task<string> GetBatchIdFromReference(string referenceId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"https://gateway.etherna.io/api/v0.3/System/postageBatchRef/{referenceId}")).ConfigureAwait(false);

            if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                return "";

            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> UploadFileVideoAsync(
            bool pinVideo,
            VideoDataResolution videoUploadDataItem,
            string batchId)
        {
            Console.WriteLine($"Uploading video {videoUploadDataItem.Resolution} in progress...");
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    var fileParameterInput = new FileParameterInput(
                        File.OpenRead(videoUploadDataItem.DownloadedFilePath!),
                        Path.GetFileName(videoUploadDataItem.DownloadedFilePath!),
                        MimeTypes.GetMimeType(Path.GetFileName(videoUploadDataItem.DownloadedFilePath!)));
                    return await beeNodeClient.GatewayClient!.UploadFileAsync(
                        batchId,
                        files: new List<FileParameterInput> { fileParameterInput },
                        swarmPin: pinVideo).ConfigureAwait(false);
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException("Some error during upload of video");
        }

        private async Task<string> UploadMetadataAsync(
            VideoData videoData,
            string batchId,
            bool swarmPin)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(videoData.Description))
                throw new InvalidOperationException("Description not defined");

            // Get all Thumbnails.
            Dictionary<string, string> thumbnailReferences = new();
            var thumbnailBestResolution = 0;
            string? downloadedThumbnailPathBestResolution = null;
            foreach (var video in videoData.VideoDataResolutions)
            {
                if (string.IsNullOrWhiteSpace(video.DownloadedThumbnailPath))
                    continue;

                using var input = File.OpenRead(video.DownloadedThumbnailPath);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);

                var keyResolution = $"{sourceImage.Width}w";
                if (!thumbnailReferences.ContainsKey(keyResolution))
                {
                    thumbnailReferences.Add(keyResolution, video.UploadedThumbnailReference!);
                    if (sourceImage.Width > thumbnailBestResolution)
                    {
                        thumbnailBestResolution = sourceImage.Width;
                        downloadedThumbnailPathBestResolution = video.DownloadedThumbnailPath;
                    }
                }
            }

            MetadataImageInput? swarmImageRaw = null;
            if (!string.IsNullOrWhiteSpace(downloadedThumbnailPathBestResolution))
            {
                using var input = File.OpenRead(videoData.VideoDataResolutions.OrderByDescending(video => video.Size).First().DownloadedThumbnailPath!);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);
                var hash = Blurhash.SkiaSharp.Blurhasher.Encode(sourceImage, 4, 4);
                swarmImageRaw = new MetadataImageInput(
                    (float)sourceImage.Width / (float)sourceImage.Height,
                    hash,
                    thumbnailReferences);
            }

            // Manifest.
            var metadataVideo = new MetadataManifestInsertInput(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userEthAddr,
                batchId,
                videoData.Description,
                videoData.VideoDataResolutions.First().Duration,
                 $"{videoData.VideoDataResolutions.First().Resolution}",
                 JsonUtility.ToJson(new MetadataPersonalDataDto { Mode = MetadataUploadMode.DevconImporter, VideoId = videoData.YoutubeId! }),
                 swarmImageRaw,
                 videoData.Title,
                 videoData.VideoDataResolutions.Select(vr => new SourceDto
                 {
                     Bitrate = vr.Bitrate,
                     Quality = vr.Resolution,
                     Reference = vr.UploadedVideoReference!,
                     Size = vr.Size
                 }).ToList());

            return await UploadMetadataAsync(metadataVideo, videoData, swarmPin).ConfigureAwait(false);
        }
        private async Task UploadThumbnailAsync(
            bool pinVideo,
            VideoDataResolution videoData,
            string batchId)
        {
            Console.WriteLine("Uploading thumbnail in progress...");

            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    var fileThumbnailParameterInput = new FileParameterInput(
                        File.OpenRead(videoData.DownloadedThumbnailPath!),
                        Path.GetFileName(videoData.DownloadedThumbnailPath!),
                        MimeTypes.GetMimeType(Path.GetFileName(videoData.DownloadedThumbnailPath!)));

                    var thumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                        batchId,
                        files: new List<FileParameterInput> { fileThumbnailParameterInput },
                        swarmPin: pinVideo).ConfigureAwait(false);

                    videoData.SetUploadedThumbnailReference(thumbnailReference);
                    return;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException("Some error during upload of thumbnail");
        }
    }
}
