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

using Etherna.BeeNet.InputModels;
using Etherna.ServicesClient.GeneratedClients.Index;
using Etherna.ServicesClient.Users;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class VideoUploaderService : IVideoUploaderService
    {
        // Const.
        private readonly long BzzDecimalPlacesToUnit = (long)Math.Pow(10, 16);
        private const int ChunkByteSize = 4096;
        private const int UploadMaxRetry = 10;
        private readonly TimeSpan UploadRetryTimeSpan = TimeSpan.FromSeconds(5);

        // Fields.
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IGatewayService gatewayService;
        private readonly VideoUploaderServiceOptions options;

        // Constructor.
        public VideoUploaderService(
            IEthernaUserIndexClient ethernaIndexClient,
            IGatewayService gatewayService,
            IOptions<VideoUploaderServiceOptions> options)
        {
            this.ethernaIndexClient = ethernaIndexClient;
            this.gatewayService = gatewayService;
            this.options = options.Value;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            Video video,
            bool pinVideo,
            bool offerVideo,
            string userEthAddress)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));

            // Create new batch.
            //calculate batch depth
            var totalSize = await video.GetTotalByteSizeAsync();
            var batchDepth = 17;
            while (Math.Pow(2, batchDepth) * ChunkByteSize < totalSize * 1.2) //keep 20% of tollerance
                batchDepth++;

            //calculate amount
            var currentPrice = await gatewayService.GetCurrentChainPriceAsync();
            var amount = (long)(options.TtlPostageStamp.TotalSeconds * currentPrice / CommonConsts.GnosisBlockTime.TotalSeconds);
            var bzzPrice = amount * Math.Pow(2, batchDepth) / BzzDecimalPlacesToUnit;

            Console.WriteLine($"Creating postage batch... Depth: {batchDepth}, Amount: {amount}, BZZ price: {bzzPrice}");

            if (!options.AcceptPurchaseOfAllBatches)
            {
                bool validSelection = false;

                while (validSelection == false)
                {
                    Console.WriteLine($"Confirm the batch purchase? Y to confirm, A to confirm all, N to deny [Y|a|n]");

                    switch (Console.ReadKey())
                    {
                        case { Key: ConsoleKey.Y }:
                        case { Key: ConsoleKey.Enter }:
                            validSelection = true;
                            break;
                        case { Key: ConsoleKey.A }:
                            options.AcceptPurchaseOfAllBatches = true;
                            validSelection = true;
                            break;
                        case { Key: ConsoleKey.N }:
                            throw new InvalidOperationException("Batch purchase denied");
                        default:
                            Console.WriteLine("Invalid selection");
                            break;
                    }
                }
            }

            //create batch
            var batchId = await gatewayService.CreatePostageBatchAsync(amount, batchDepth);

            Console.WriteLine($"Postage batch: {batchId}");

            // Upload video files.
            foreach (var encodedFile in video.EncodedFiles.OfType<SourceFile>())
            {
                Console.WriteLine(encodedFile switch
                {
                    AudioSourceFile _ => "Uploading audio track in progress...",
                    VideoSourceFile evf => $"Uploading video track {evf.VideoQualityLabel} in progress...",
                    _ => throw new InvalidOperationException()
                });

                var uploadSucceeded = false;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        var fileParameterInput = new FileParameterInput(
                            (await encodedFile.ReadAsStreamAsync()).Stream,
                            encodedFile.TryGetFileName(),
                            "video/mp4");

                        encodedFile.SetSwarmHash(await gatewayService.UploadFilesAsync(
                            batchId,
                            files: new List<FileParameterInput> { fileParameterInput },
                            pinVideo));
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
                    await gatewayService.OfferContentAsync(encodedFile.SwarmHash!);
            }

            // Upload thumbnail.
            Console.WriteLine("Uploading thumbnail in progress...");
            foreach (var thumbnailFile in video.ThumbnailFiles.OfType<SourceFile>())
            {
                var uploadSucceeded = false;
                string thumbnailReference = null!;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        var fileThumbnailParameterInput = new FileParameterInput(
                            (await thumbnailFile.ReadAsStreamAsync()).Stream,
                            thumbnailFile.TryGetFileName(),
                            "image/jpeg");

                        thumbnailReference = await gatewayService.UploadFilesAsync(
                            batchId,
                            new List<FileParameterInput> { fileThumbnailParameterInput },
                            pinVideo);
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
                    await gatewayService.OfferContentAsync(thumbnailReference);
            }

            // Manifest.
            var metadataVideo = await ManifestDto.BuildNewAsync(video, batchId, userEthAddress);
            {
                var uploadSucceeded = false;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        video.EthernaPermalinkHash = await UploadVideoManifestAsync(metadataVideo, pinVideo, offerVideo);
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

            Console.WriteLine($"Published with swarm hash (permalink): {video.EthernaPermalinkHash}");

            // List on index.
            if (options.IndexManifest)
            {
                if (video.EthernaIndexId is null)
                    video.EthernaIndexId = await ethernaIndexClient.VideosClient.VideosPostAsync(
                        new VideoCreateInput
                        {
                            ManifestHash = video.EthernaPermalinkHash!,
                        });
                else
                    await ethernaIndexClient.VideosClient.VideosPutAsync(video.EthernaIndexId, video.EthernaPermalinkHash!);
            }

            Console.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
        }

        public async Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest,
            bool offerManifest)
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

                    manifestReference = await gatewayService.UploadFilesAsync(
                        videoManifest.BatchId,
                        new[] {
                            new FileParameterInput(
                                manifestStream,
                                "metadata.json",
                                "application/json")
                        },
                        pinManifest);
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

            if (offerManifest)
                await gatewayService.OfferContentAsync(manifestReference);

            return manifestReference;
        }
    }
}
