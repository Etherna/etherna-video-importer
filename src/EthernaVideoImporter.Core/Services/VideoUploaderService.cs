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
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class VideoUploaderService : IVideoUploaderService
    {
        // Const.
        private readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);
        private readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);

        // Fields.
        private readonly BeeNodeClient beeNodeClient;
        private readonly IUserGatewayClient ethernaGatewayClient;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly TimeSpan ttlPostageStamp;
        private readonly string userEthAddr;

        // Constructor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            IUserGatewayClient ethernaGatewayClient,
            IUserIndexClient ethernaIndexClient,
            string userEthAddr,
            TimeSpan ttlPostageStamp)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));

            this.beeNodeClient = beeNodeClient;
            this.ethernaGatewayClient = ethernaGatewayClient;
            this.ethernaIndexClient = ethernaIndexClient;
            this.userEthAddr = userEthAddr;
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

            // Create new batch.
            Console.WriteLine("Creating batch...");

            //calculate batch deep
            var totalSize = video.GetTotalByteSize();
            var batchDeep = 17;
            while (Math.Pow(2, batchDeep) * 4096 < totalSize * 1.2) //keep 20% of tollerance
                batchDeep++;

            //calculate amount
            var chainState = await ethernaGatewayClient.SystemClient.ChainstateAsync();
            var amount = (long)(ttlPostageStamp.TotalSeconds * chainState.CurrentPrice / CommonConsts.GnosisBlockTime.TotalSeconds);

            //create batch
            var batchId = await CreatePostageBatchAsync(batchDeep, amount);

            // Upload video files.
            foreach (var encodedFile in video.EncodedFiles)
            {
                Console.WriteLine(encodedFile switch
                {
                    AudioFile _ => "Uploading audio track in progress...",
                    VideoFile evf => $"Uploading video track {evf.VideoQualityLabel} in progress...",
                    _ => throw new InvalidOperationException()
                });

                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(encodedFile.DownloadedFilePath),
                    Path.GetFileName(encodedFile.DownloadedFilePath),
                    MimeTypes.GetMimeType(Path.GetFileName(encodedFile.DownloadedFilePath)));

                encodedFile.UploadedHashReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    batchId,
                    files: new List<FileParameterInput> { fileParameterInput },
                    swarmPin: pinVideo);

                if (offerVideo)
                    await ethernaGatewayClient.ResourcesClient.OffersPostAsync(encodedFile.UploadedHashReference);
            }

            // Upload thumbnail.
            if (video.ThumbnailFile is not null)
            {
                Console.WriteLine("Uploading thumbnail in progress...");

                var fileThumbnailParameterInput = new FileParameterInput(
                    File.OpenRead(video.ThumbnailFile.DownloadedFilePath),
                    Path.GetFileName(video.ThumbnailFile.DownloadedFilePath),
                    MimeTypes.GetMimeType(Path.GetFileName(video.ThumbnailFile.DownloadedFilePath)));

                var thumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    batchId,
                    files: new List<FileParameterInput> { fileThumbnailParameterInput },
                    swarmPin: pinVideo);

                video.ThumbnailFile.UploadedHashReference = thumbnailReference;

                if (offerVideo)
                    await ethernaGatewayClient.ResourcesClient.OffersPostAsync(video.ThumbnailFile.UploadedHashReference);
            }

            // Manifest.
            var metadataVideo = new ManifestDto(video, batchId, userEthAddr);
            video.EthernaPermalinkHash = await UploadVideoManifestAsync(metadataVideo, pinVideo);

            if (offerVideo)
                await ethernaGatewayClient.ResourcesClient.OffersPostAsync(video.EthernaPermalinkHash);

            // List on index.
            Console.WriteLine($"List on Etherna index: {video.EthernaPermalinkHash}");

            video.EthernaIndexId = await ethernaIndexClient.VideosClient.VideosPostAsync(
                new VideoCreateInput
                {
                    ManifestHash = video.EthernaPermalinkHash,
                });
        }

        public async Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest)
        {
            if (videoManifest is null)
                throw new ArgumentNullException(nameof(videoManifest));

            var serializedManifest = JsonSerializer.Serialize(videoManifest);
            using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedManifest));

            // Upload manifest.
            return await beeNodeClient.GatewayClient!.UploadFileAsync(
                videoManifest.BatchId,
                files: new[] {
                    new FileParameterInput(
                        manifestStream,
                        "metadata.json",
                        "application/json")
                },
                swarmPin: pinManifest);
        }

        // Helpers.
        private async Task<string> CreatePostageBatchAsync(int batchDeep, long amount)
        {
            var batchReferenceId = await ethernaGatewayClient.UsersClient.BatchesPostAsync(batchDeep, amount);

            // Wait until created batch is avaiable.
            Console.WriteLine("Waiting for batch ready... (it may take a while)");

            var batchStartWait = DateTime.UtcNow;
            string? batchId = null;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchCreationTimeout)
                {
                    var ex = new InvalidOperationException("Batch not avaiable after timeout");
                    ex.Data.Add("BatchReferenceId", batchReferenceId);
                    throw ex;
                }

                try
                {
                    batchId = await ethernaGatewayClient.SystemClient.PostageBatchRefAsync(batchReferenceId);
                }
                catch (GatewayApiException)
                {
                    //waiting for batchId avaiable
                    await Task.Delay(BatchCheckTimeSpan);
                }
            } while (string.IsNullOrWhiteSpace(batchId));

            // Wait until created batch is usable.
            batchStartWait = DateTime.UtcNow;
            bool batchIsUsable;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchUsableTimeout)
                {
                    var ex = new InvalidOperationException("Batch not usable after timeout");
                    ex.Data.Add("BatchId", batchId);
                    throw ex;
                }

                batchIsUsable = (await ethernaGatewayClient.UsersClient.BatchesGetAsync(batchId)).Usable;

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            return batchId;
        }
    }
}
