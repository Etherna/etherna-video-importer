// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeeNet.Hasher.Postage;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
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
        private const string ManifestFileName = "metadata.json";
        private const string ManifestMimeType = "application/json";
        private const string ThumbnailMimeType = "image/jpeg";
        private const int UploadMaxRetry = 10;
        private readonly TimeSpan UploadRetryTimeSpan = TimeSpan.FromSeconds(5);
        private const string VideoMimeType = "video/mp4";

        // Fields.
        private readonly IAppVersionService appVersionService;
        private readonly ICalculatorService calculatorService;
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IGatewayService gatewayService;
        private readonly IIoService ioService;
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly VideoUploaderServiceOptions options;

        // Constructor.
        public VideoUploaderService(
            IAppVersionService appVersionService,
            ICalculatorService calculatorService,
            IEthernaUserIndexClient ethernaIndexClient,
            IGatewayService gatewayService,
            IIoService ioService,
            IOptions<VideoUploaderServiceOptions> options)
        {
            this.appVersionService = appVersionService;
            this.calculatorService = calculatorService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.gatewayService = gatewayService;
            this.ioService = ioService;
            this.options = options.Value;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            Video video,
            bool pinVideo,
            bool offerVideo,
            string userEthAddress)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));

            // Create new batch.
            //calculate batch depth and amount
            ioService.Write("Calculating required postage batch depth... ");
            var batchDepth = await GetMinBatchDepthAsync(video, userEthAddress);
            ioService.WriteLine("Done");

            var currentPrice = await gatewayService.GetChainPriceAsync();
            ioService.WriteLine($"Current chain price: {currentPrice.ToPlurString()}");
            
            var amount = PostageBatch.CalculateAmount(currentPrice, options.TtlPostageStamp);
            var bzzPrice = PostageBatch.CalculatePrice(amount, batchDepth);

            //user confirmation
            ioService.WriteLine($"Creating postage batch... Depth: {batchDepth}, Amount: {amount.ToPlurString()}, BZZ price: {bzzPrice}");
            if (!options.AcceptPurchaseOfAllBatches)
            {
                bool validSelection = false;

                while (validSelection == false)
                {
                    ioService.WriteLine($"Confirm the batch purchase? Y to confirm, A to confirm all, N to deny [Y|a|n]");

                    switch (ioService.ReadKey())
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
                            ioService.WriteLine("Invalid selection");
                            break;
                    }
                }
            }

            //create batch
            var batchId = await gatewayService.CreatePostageBatchAsync(amount, batchDepth);
            ioService.WriteLine($"Postage batch: {batchId}");

            // Upload video files.
            foreach (var encodedFile in video.EncodedFiles.OfType<SourceFile>())
            {
                ioService.WriteLine(encodedFile switch
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
                        encodedFile.SetSwarmHash(await gatewayService.UploadFileAsync(
                            batchId,
                            (await encodedFile.ReadToStreamAsync()).Stream,
                            encodedFile.TryGetFileName(),
                            VideoMimeType,
                            pinVideo));
                        uploadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        ioService.WriteErrorLine("Error uploading video file");
                        ioService.PrintException(ex);
                        if (i + 1 < UploadMaxRetry)
                        {
                            ioService.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

                if (offerVideo)
                    await gatewayService.FundResourceDownloadAsync(encodedFile.SwarmHash!.Value);
            }

            // Upload thumbnail.
            ioService.WriteLine("Uploading thumbnail in progress...");
            foreach (var thumbnailFile in video.ThumbnailFiles.OfType<SourceFile>())
            {
                var uploadSucceeded = false;
                SwarmHash thumbnailReference = default;
                for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
                {
                    try
                    {
                        thumbnailReference = await gatewayService.UploadFileAsync(
                            batchId,
                            (await thumbnailFile.ReadToStreamAsync()).Stream,
                            thumbnailFile.TryGetFileName(),
                            ThumbnailMimeType,
                            pinVideo);
                        uploadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        ioService.WriteErrorLine("Error uploading thumbnail file");
                        ioService.PrintException(ex);
                        if (i + 1 < UploadMaxRetry)
                        {
                            ioService.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

                thumbnailFile.SetSwarmHash(thumbnailReference);

                if (offerVideo)
                    await gatewayService.FundResourceDownloadAsync(thumbnailReference);
            }

            // Upload manifest.
            var metadataVideo = await ManifestDto.BuildNewAsync(video, batchId, userEthAddress, appVersionService.CurrentVersion);
            video.EthernaPermalinkHash = await UploadVideoManifestAsync(metadataVideo, pinVideo, offerVideo);

            ioService.WriteLine($"Published with swarm hash (permalink): {video.EthernaPermalinkHash}");

            // List on index.
            if (video.EthernaIndexId is null)
                video.EthernaIndexId = await ethernaIndexClient.PublishNewVideoAsync(video.EthernaPermalinkHash!.Value);
            else
                await ethernaIndexClient.UpdateVideoManifestAsync(video.EthernaIndexId, video.EthernaPermalinkHash!.Value);

            ioService.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
        }

        public async Task<SwarmHash> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest,
            bool offerManifest)
        {
            ArgumentNullException.ThrowIfNull(videoManifest, nameof(videoManifest));

            // Upload manifest.
            var uploadSucceeded = false;
            SwarmHash manifestReference = default;
            for (int i = 0; i < UploadMaxRetry && !uploadSucceeded; i++)
            {
                try
                {
                    var serializedManifest = JsonSerializer.Serialize(videoManifest, jsonSerializerOptions);
                    using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedManifest));

                    manifestReference = await gatewayService.UploadFileAsync(
                        videoManifest.BatchId,
                        manifestStream,
                        ManifestFileName,
                        ManifestMimeType,
                        pinManifest);
                    uploadSucceeded = true;
                }
                catch (Exception ex)
                {
                    ioService.WriteErrorLine("Error uploading manifest file");
                    ioService.PrintException(ex);
                    if (i + 1 < UploadMaxRetry)
                    {
                        ioService.WriteLine("Retry...");
                        await Task.Delay(UploadRetryTimeSpan);
                    }
                }
            }
            if (!uploadSucceeded)
                throw new InvalidOperationException($"Can't upload file after {UploadMaxRetry} retries");

            if (offerManifest)
                await gatewayService.FundResourceDownloadAsync(manifestReference);

            return manifestReference;
        }
        
        // Helpers.
        private async Task<int> GetMinBatchDepthAsync(
            Video video,
            string userEthAddress)
        {
            var stampIssuer = new PostageStampIssuer(PostageBatch.MaxDepthInstance);
            
            //evaluate video encoded files
            foreach (var encodedFile in video.EncodedFiles.OfType<SourceFile>())
            {
                var (stream, _) = await encodedFile.ReadToStreamAsync();
                await calculatorService.EvaluateFileUploadAsync(
                    stream,
                    VideoMimeType,
                    encodedFile.TryGetFileName(),
                    postageStampIssuer: stampIssuer);
                await stream.DisposeAsync();
            }
            
            //evaluate thumbnail files
            foreach (var thumbnailFile in video.ThumbnailFiles.OfType<SourceFile>())
            {
                var (stream, _) = await thumbnailFile.ReadToStreamAsync();
                await calculatorService.EvaluateFileUploadAsync(
                    stream,
                    ThumbnailMimeType,
                    thumbnailFile.TryGetFileName(),
                    postageStampIssuer: stampIssuer);
                await stream.DisposeAsync();
            }
            
            //evaluate manifest
            var manifest = await ManifestDto.BuildNewAsync(
                video,
                PostageBatchId.Zero,
                userEthAddress,
                appVersionService.CurrentVersion,
                allowFakeReferences: true);
            var serializedManifest = JsonSerializer.Serialize(manifest, jsonSerializerOptions);
            using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedManifest));

            var result = await calculatorService.EvaluateFileUploadAsync(
                manifestStream,
                ManifestMimeType,
                ManifestFileName,
                postageStampIssuer: stampIssuer);

            return result.RequiredPostageBatchDepth;
        }
    }
}
