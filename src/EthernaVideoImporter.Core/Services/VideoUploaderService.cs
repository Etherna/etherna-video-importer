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
using Etherna.ServicesClient.Clients.Gateway;
using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Dtos;
using Etherna.VideoImporter.Core.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class VideoUploaderService : IVideoUploaderService
    {
        // Const.
        private readonly TimeSpan BATCH_CHECK_TIME = new(0, 0, 0, 10);
        private readonly TimeSpan BATCH_TIMEOUT_TIME = new(0, 0, 7, 0);
        private const int BLOCK_TIME = 5;
        private const int MAX_RETRY = 3;
        private const int WAITING_PROPAGATION_BATCH_SECONDS = 5000; // ms.
        private const int WAITING_PROPAGATION_BATCH_RETRY = 50; // retry.

        // Fields.
        private readonly BeeNodeClient beeNodeClient;
        private readonly IUserGatewayClient ethernaGatewayClient;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly HttpClient httpClient;
        private readonly bool includeAudioTrack;
        private readonly int ttlPostageStamp;
        private readonly string userEthAddr;

        // Constructor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            IUserGatewayClient ethernaGatewayClient,
            IUserIndexClient ethernaIndexClient,
            string userEthAddr,
            HttpClient httpClient,
            bool includeAudioTrack,
            int ttlPostageStamp)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));

            this.beeNodeClient = beeNodeClient;
            this.ethernaGatewayClient = ethernaGatewayClient;
            this.ethernaIndexClient = ethernaIndexClient;
            this.userEthAddr = userEthAddr;
            this.httpClient = httpClient;
            this.includeAudioTrack = includeAudioTrack;
            this.ttlPostageStamp = ttlPostageStamp;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            Video video,
            bool pinVideo,
            bool offerVideo)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));
            if (!video.EncodedFiles.Any())
                throw new ArgumentException("Must exists at least an encoded file", nameof(video));

            // Create new batch.
            Console.WriteLine("Create batch...");

            // Size of all video to upload.
            var totalSize = video.EncodedFiles.Sum(v => v.ByteSize);

            // Calculate batch deep.
            var batchDeep = 17;
            while (Math.Pow(2, batchDeep) * 4 * 1000 < totalSize)
            {
                batchDeep++;
                if (batchDeep > 64)
                    throw new InvalidOperationException("Batch deep exceeds the maximum");
            }

            var chainState = await ethernaGatewayClient.SystemClient.ChainstateAsync().ConfigureAwait(false);
            var amount = (long)new TimeSpan(ttlPostageStamp * 24, 0, 0).TotalSeconds * chainState.CurrentPrice / BLOCK_TIME;
            var batchReferenceId = await ethernaGatewayClient.UsersClient.BatchesPostAsync(batchDeep, amount).ConfigureAwait(false);

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

                var httpResponse = await httpClient.GetAsync(new Uri($"https://gateway.etherna.io/api/v0.3/System/postageBatchRef/{batchReferenceId}")).ConfigureAwait(false);

                batchId = httpResponse.StatusCode != System.Net.HttpStatusCode.OK ? "" :
                    await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (string.IsNullOrWhiteSpace(batchId));

            // Check and wait until created batch is usable.
            timeWaited = 0;
            bool batchUsable = false;
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

                //waiting propagation time on bee
                for (int i = 0; i <= WAITING_PROPAGATION_BATCH_RETRY; i++)
                {
                    if (i == WAITING_PROPAGATION_BATCH_RETRY)
                        throw new InvalidOperationException($"Some error during get batch status");

                    try
                    {
                        batchUsable = (await ethernaGatewayClient.UsersClient.BatchesGetAsync(batchId).ConfigureAwait(false)).Usable;
                        break;
                    }
                    catch { await Task.Delay(WAITING_PROPAGATION_BATCH_SECONDS).ConfigureAwait(false); }
                }

                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (!batchUsable);

            // Upload video file.
            foreach (var encodedFile in video.EncodedFiles)
            {
                // Upload video.
                Console.WriteLine(encodedFile switch
                {
                    AudioFile _ => "Uploading audio track in progress...",
                    VideoFile evf => $"Uploading video track {evf.VideoQualityLabel} in progress...",
                    _ => throw new InvalidOperationException()
                });
                var k = 0;
                while (k <= MAX_RETRY)
                {
                    if (k == MAX_RETRY)
                        throw new InvalidOperationException("Some error during upload of video");

                    try
                    {
                        k++;
                        var fileParameterInput = new FileParameterInput(
                            File.OpenRead(encodedFile.DownloadedFilePath!),
                            Path.GetFileName(encodedFile.DownloadedFilePath!),
                            MimeTypes.GetMimeType(Path.GetFileName(encodedFile.DownloadedFilePath!)));

                        encodedFile.UploadedHashReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                            batchId,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: pinVideo).ConfigureAwait(false);
                        break;
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                }

                if (offerVideo && encodedFile.UploadedHashReference is not null)
                    await ethernaGatewayClient.ResourcesClient.OffersPostAsync(encodedFile.UploadedHashReference).ConfigureAwait(false);
            }

            // Upload thumbnail.
            if (video.ThumbnailFile is not null)
            {
                Console.WriteLine("Uploading thumbnail in progress...");

                var j = 0;
                while (j <= MAX_RETRY)
                {
                    if (j == MAX_RETRY)
                        throw new InvalidOperationException("Some error during upload of thumbnail");

                    try
                    {
                        j++;
                        var fileThumbnailParameterInput = new FileParameterInput(
                            File.OpenRead(video.ThumbnailFile.DownloadedFilePath),
                            Path.GetFileName(video.ThumbnailFile.DownloadedFilePath),
                            MimeTypes.GetMimeType(Path.GetFileName(video.ThumbnailFile.DownloadedFilePath)));

                        var thumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                            batchId,
                            files: new List<FileParameterInput> { fileThumbnailParameterInput },
                            swarmPin: pinVideo).ConfigureAwait(false);

                        video.ThumbnailFile.UploadedHashReference = thumbnailReference;
                        break;
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                }

                if (offerVideo && video.ThumbnailFile.UploadedHashReference is not null)
                    await ethernaGatewayClient.ResourcesClient.OffersPostAsync(video.ThumbnailFile.UploadedHashReference).ConfigureAwait(false);
            }

            // Upload metadata.
            if (string.IsNullOrWhiteSpace(video.Metadata.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(video.Metadata.Description))
                throw new InvalidOperationException("Description not defined");

            // Get all Thumbnails.
            Dictionary<string, string> thumbnailReferences = new();
            var thumbnailBestResolution = 0;
            string? downloadedThumbnailPathBestResolution = null;

            if (!string.IsNullOrWhiteSpace(video.ThumbnailFile?.DownloadedFilePath))
            {

                using var input = File.OpenRead(video.ThumbnailFile.DownloadedFilePath);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);

                var keyResolution = $"{sourceImage.Width}w";
                if (!thumbnailReferences.ContainsKey(keyResolution))
                {
                    thumbnailReferences.Add(keyResolution, video.ThumbnailFile.UploadedHashReference!);
                    if (sourceImage.Width > thumbnailBestResolution)
                    {
                        thumbnailBestResolution = sourceImage.Width;
                        downloadedThumbnailPathBestResolution = video.ThumbnailFile.DownloadedFilePath;
                    }
                }
            }

            ManifestThumbnailDto? swarmImageRaw = null;
            if (!string.IsNullOrWhiteSpace(downloadedThumbnailPathBestResolution))
            {
                using var input = File.OpenRead(video.ThumbnailFile!.DownloadedFilePath!);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);
                var hash = Blurhash.SkiaSharp.Blurhasher.Encode(sourceImage, 4, 4);
                swarmImageRaw = new ManifestThumbnailDto(
                    (float)sourceImage.Width / (float)sourceImage.Height,
                    hash,
                    thumbnailReferences);
            }

            // Manifest.
            var metadataVideo = new ManifestDto(
                video.Metadata.Title,
                video.Metadata.Description,
                $"{video.EncodedFiles.OfType<VideoFile>().First().VideoQualityLabel}",
                userEthAddr,
                (long)video.Metadata.Duration.TotalSeconds,
                swarmImageRaw!,
                video.EncodedFiles.OfType<VideoFile>().Select(vf => new ManifestVideoSourceDto(vf)).ToList(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                null,
                batchId,
                JsonSerializer.Serialize(ManifestPersonalDataDto.BuildNew(video.Metadata.Id)));

            var hashMetadataReference = await UploadVideoManifestAsync(metadataVideo, pinVideo).ConfigureAwait(false);
            video.EthernaPermalinkHash = hashMetadataReference;

            if (offerVideo)
                await ethernaGatewayClient.ResourcesClient.OffersPostAsync(hashMetadataReference).ConfigureAwait(false);

            // Sync Index.
            Console.WriteLine("Video indexing in progress...");
            if (!string.IsNullOrEmpty(video.EthernaIndexId))
            {
                // Update manifest index.
                Console.WriteLine($"Update Index: {video!.EthernaIndexId}\t{hashMetadataReference}");

                await ethernaIndexClient.VideosClient.VideosPutAsync(video.EthernaIndexId!, hashMetadataReference).ConfigureAwait(false);
            }
            else
            {
                // Create new manifest index.
                Console.WriteLine($"Create Index: {hashMetadataReference}");

                var videoCreateInput = new VideoCreateInput
                {
                    ManifestHash = hashMetadataReference,
                };
                var indexVideoId = await ethernaIndexClient.VideosClient.VideosPostAsync(videoCreateInput).ConfigureAwait(false);

                video.EthernaIndexId = indexVideoId;
            }
        }

        public async Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest)
        {
            if (videoManifest is null)
                throw new ArgumentNullException(nameof(videoManifest));

            var tmpMetadata = Path.GetTempFileName();
            var hashMetadataReference = "";
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, JsonSerializer.Serialize(videoManifest)).ConfigureAwait(false);

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
                            videoManifest.BatchId,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: pinManifest).ConfigureAwait(false);
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                if (string.IsNullOrWhiteSpace(hashMetadataReference))
                    throw new InvalidOperationException("Some error during upload of metadata");
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }

            return hashMetadataReference;
        }
    }
}
