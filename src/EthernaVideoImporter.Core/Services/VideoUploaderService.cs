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
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class VideoUploaderService : IVideoUploaderService
    {
        // Const.
        private readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        private readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);
        private readonly long BzzDecimalPlacesToUnit = (long)Math.Pow(10, 16);
        private const int ChunkByteSize = 4096;
        private const int UploadMaxRetry = 10;
        private readonly TimeSpan UploadRetryTimeSpan = TimeSpan.FromSeconds(5);

        // Fields.
        private readonly BeeNodeClient beeNodeClient;
        private bool acceptPurchaseOfAllBatches;
        private readonly IGatewayService gatewayClient;
        private readonly IUserIndexClient ethernaIndexClient;
        private readonly TimeSpan ttlPostageStamp;
        private readonly string userEthAddr;

        // Constructor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            IGatewayService gatewayClient,
            IUserIndexClient ethernaIndexClient,
            string userEthAddr,
            TimeSpan ttlPostageStamp,
            bool confirmPurchaseAllBatches)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));

            this.beeNodeClient = beeNodeClient;
            this.acceptPurchaseOfAllBatches = confirmPurchaseAllBatches;
            this.gatewayClient = gatewayClient;
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
            //calculate batch deep
            var totalSize = video.GetTotalByteSize();
            var batchDeep = 17;
            while (Math.Pow(2, batchDeep) * ChunkByteSize < totalSize * 1.2) //keep 20% of tollerance
                batchDeep++;

            //calculate amount
            var currentPrice = await gatewayClient.ChainCurrentPriceAsync();
            var amount = (long)(ttlPostageStamp.TotalSeconds * currentPrice / CommonConsts.GnosisBlockTime.TotalSeconds);
            var bzzPrice = amount * Math.Pow(2, batchDeep) / BzzDecimalPlacesToUnit;

            Console.WriteLine($"Creating postage batch... Depth: {batchDeep} Amount: {amount} BZZ price: {bzzPrice}");

            if (!acceptPurchaseOfAllBatches)
            {
                bool validSelection = false;

                while (validSelection == false)
                {
                    Console.WriteLine($"Confirm the batch purchase? Y to confirm, A to confirm all, N to deny [Y|a|n]");

                    switch (Console.ReadKey())
                    {
                        case ConsoleKeyInfo yk when yk.Key == ConsoleKey.Y:
                            validSelection = true;
                            break;
                        case ConsoleKeyInfo ak when ak.Key == ConsoleKey.A:
                            acceptPurchaseOfAllBatches = true;
                            validSelection = true;
                            break;
                        case ConsoleKeyInfo nk when nk.Key == ConsoleKey.N:
                            throw new InvalidOperationException("Batch purchase denied");
                        default:
                            Console.WriteLine("Invalid selection");
                            break;
                    }
                }
            }

            //create batch
            var batchId = await gatewayClient.CreatePostageBatchAsync(batchDeep, amount);
            await WaitForBatchUsable(batchId);
            Console.WriteLine($"Postage batch: {batchId}");

            // Upload video files.
            foreach (var encodedFile in video.EncodedFiles.OfType<LocalFileBase>())
            {
                Console.WriteLine(encodedFile switch
                {
                    AudioLocalFile _ => "Uploading audio track in progress...",
                    VideoLocalFile evf => $"Uploading video track {evf.VideoQualityLabel} in progress...",
                    _ => throw new InvalidOperationException()
                });

                var uploadSucceeded = false;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        var fileParameterInput = new FileParameterInput(
                            File.OpenRead(encodedFile.FilePath),
                            Path.GetFileName(encodedFile.FilePath),
                            "video/mp4");

                        encodedFile.SetSwarmHash(await beeNodeClient.GatewayClient!.UploadFileAsync(
                            batchId,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: pinVideo));
                        uploadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        if (i + 1 < UploadMaxRetry)
                        {
                            Console.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

                if (offerVideo)
                    await gatewayClient.OffersPostAsync(encodedFile.SwarmHash!);
            }

            // Upload thumbnail.
            Console.WriteLine("Uploading thumbnail in progress...");
            foreach (var thumbnailFile in video.ThumbnailFiles.OfType<LocalFileBase>())
            {
                var uploadSucceeded = false;
                string thumbnailReference = null!;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        var fileThumbnailParameterInput = new FileParameterInput(
                            File.OpenRead(thumbnailFile.FilePath),
                            Path.GetFileName(thumbnailFile.FilePath),
                            "image/jpeg");

                        thumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                            batchId,
                            files: new List<FileParameterInput> { fileThumbnailParameterInput },
                            swarmPin: pinVideo);
                        uploadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        if (i + 1 < UploadMaxRetry)
                        {
                            Console.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

                thumbnailFile.SetSwarmHash(thumbnailReference);

                if (offerVideo)
                    await gatewayClient.OffersPostAsync(thumbnailReference);
            }

            // Manifest.
            var metadataVideo = new ManifestDto(video, batchId, userEthAddr);
            {
                var uploadSucceeded = false;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        video.EthernaPermalinkHash = await UploadVideoManifestAsync(metadataVideo, pinVideo);
                        uploadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        if (i + 1 < UploadMaxRetry)
                        {
                            Console.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");
            }

            if (offerVideo)
                await gatewayClient.OffersPostAsync(video.EthernaPermalinkHash!);

            Console.WriteLine($"Published with swarm hash (permalink): {video.EthernaPermalinkHash}");

            // List on index.
            if (video.EthernaIndexId is null)
                video.EthernaIndexId = await ethernaIndexClient.VideosClient.VideosPostAsync(
                    new VideoCreateInput
                    {
                        ManifestHash = video.EthernaPermalinkHash!,
                    });
            else
                await ethernaIndexClient.VideosClient.VideosPutAsync(video.EthernaIndexId, video.EthernaPermalinkHash!);

            Console.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
        }

        private async Task WaitForBatchUsable(string batchId)
        {
            // Wait until created batch is usable.
            Console.Write("Waiting for batch being usable... (it may take a while)");

            var batchStartWait = DateTime.UtcNow;
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

                batchIsUsable = await gatewayClient.BatchIsUsableAsync(batchId);

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            Console.WriteLine(". Done");
        }

        public async Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest)
        {
            if (videoManifest is null)
                throw new ArgumentNullException(nameof(videoManifest));

            // Upload manifest.
            var uploadSucceeded = false;
            string manifestReference = null!;
            for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
            {
                try
                {
                    var serializedManifest = JsonSerializer.Serialize(videoManifest, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedManifest));

                    manifestReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                        videoManifest.BatchId,
                        files: new[] {
                            new FileParameterInput(
                                manifestStream,
                                "metadata.json",
                                "application/json")
                        },
                        swarmPin: pinManifest);
                    uploadSucceeded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (i + 1 < UploadMaxRetry)
                    {
                        Console.WriteLine("Retry...");
                        await Task.Delay(UploadRetryTimeSpan);
                    }
                }
            }
            if (!uploadSucceeded)
                throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

            return manifestReference;
        }

        // Helpers.
        /*
        private async Task<string> CreatePostageBatchAsync(int batchDeep, long amount)
        {
            var batchReferenceId = await gatewayClient.UsersClient.BatchesPostAsync(batchDeep, amount);

            // Wait until created batch is avaiable.
            Console.Write("Waiting for batch created... (it may take a while)");

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
                    batchId = await gatewayClient.SystemClient.PostageBatchRefAsync(batchReferenceId);
                }
                catch (GatewayApiException)
                {
                    //waiting for batchId avaiable
                    await Task.Delay(BatchCheckTimeSpan);
                }
            } while (string.IsNullOrWhiteSpace(batchId));

            Console.WriteLine(". Done");

            // Wait until created batch is usable.
            Console.Write("Waiting for batch being usable... (it may take a while)");

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

                batchIsUsable = (await gatewayClient.UsersClient.BatchesGetAsync(batchId)).Usable;

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            Console.WriteLine(". Done");

            return batchId;
        }
        */
    }
}
