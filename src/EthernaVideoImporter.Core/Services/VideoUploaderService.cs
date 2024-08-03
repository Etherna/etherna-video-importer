// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.Sdk.Users.Index.Models;
using Etherna.Sdk.Users.Index.Services;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class VideoUploaderService : IVideoUploaderService
    {
        // Const.
        private const string ChunksSubDirectoryName = "chunks";
        private const int UploadMaxRetry = 10;
        private readonly TimeSpan UploadRetryTimeSpan = TimeSpan.FromSeconds(5);

        // Fields.
        private readonly IAppVersionService appVersionService;
        private readonly IChunkService chunkService;
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly IGatewayService gatewayService;
        private readonly IIoService ioService;
        private readonly VideoUploaderServiceOptions options;
        private readonly IVideoPublisherService videoPublisherService;

        // Constructor.
        public VideoUploaderService(
            IAppVersionService appVersionService,
            IChunkService chunkService,
            IEthernaUserIndexClient ethernaIndexClient,
            IGatewayService gatewayService,
            IIoService ioService,
            IOptions<VideoUploaderServiceOptions> options,
            IVideoPublisherService videoPublisherService)
        {
            this.appVersionService = appVersionService;
            this.chunkService = chunkService;
            this.ethernaIndexClient = ethernaIndexClient;
            this.gatewayService = gatewayService;
            this.ioService = ioService;
            this.videoPublisherService = videoPublisherService;
            this.options = options.Value;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            Video video,
            bool fundPinning,
            bool fundDownload,
            string ownerEthAddress)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));
            
            // Create chunks. Do as first thing, also to evaluate required postage batch depth.
            var chunksDirectory = Directory.CreateDirectory(Path.Combine(CommonConsts.TempDirectory.FullName, ChunksSubDirectoryName));
            var stampIssuer = new PostageStampIssuer(PostageBatch.MaxDepthInstance);
            
            //video source files, exclude already uploaded Swarm files 
            foreach (var videoFile in video.VideoFiles.OfType<VideoFile>())
            {
                ioService.WriteLine($"Creating chunks of video stream {videoFile.QualityLabel} in progress...");
                
                using var stream = await videoFile.ReadToStreamAsync();
                var streamHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
                videoFile.SwarmHash = streamHash;
            }
            
            //thumbnail source files, exclude already uploaded Swarm files 
            ioService.WriteLine($"Creating chunks of thumbnail in progress...");
            foreach (var thumbnailFile in video.ThumbnailFiles.OfType<ThumbnailFile>())
            {
                using var stream = await thumbnailFile.ReadToStreamAsync();
                var streamHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
                thumbnailFile.SwarmHash = streamHash;
            }
            
            //new video manifest (at first without batchId. See: https://etherna.atlassian.net/browse/EVMS-8).
            //personal data
            var personalData = new VideoManifestPersonalData(
                CommonConsts.ImporterIdentifier,
                appVersionService.CurrentVersion.ToString(),
                video.Metadata.SourceName,
                video.Metadata.SourceId);
            
            //video manifest video sources
            var manifestVideoSources = video.VideoFiles.Select(v =>new VideoManifestVideoSource(
                v.FileName,
                v.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here"),
                v.VideoType,
                v.QualityLabel,
                v.ByteSize));
            
            //video manifest thumbnail
            var manifestThumbnail = new VideoManifestImage(
                video.AspectRatio,
                video.ThumbnailBlurhash,
                video.ThumbnailFiles.Select(t => new VideoManifestImageSource(
                    t.FileName,
                    t.ImageType,
                    t.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here"),
                    t.Width)));
            
            //video manifest
            var videoManifest = new VideoManifest(
                video.AspectRatio,
                batchId: null,
                DateTimeOffset.Now,
                video.Metadata.Description,
                video.Metadata.Duration,
                video.Metadata.Title,
                ownerEthAddress,
                personalData.Serialize(),
                manifestVideoSources,
                manifestThumbnail);

            await videoPublisherService.CreateVideoManifestChunksAsync(
                videoManifest,
                chunksDirectory.FullName,
                postageStampIssuer: stampIssuer);
            
            // Create new batch.
            var batchId = await CreatePostageBatchAsync(stampIssuer.Buckets.RequiredPostageBatchDepth);
            
            // Assign batchId to manifest, and re-create manifest chunks. Get final hash.
            videoManifest.BatchId = batchId;
            var videoManifestHash = await videoPublisherService.CreateVideoManifestChunksAsync(
                videoManifest,
                chunksDirectory.FullName,
                postageStampIssuer: stampIssuer);
            
            video.EthernaPermalinkHash = videoManifestHash;
            
            // Upload chunks. Pin only video manifest hash, if required.
            var chunkFiles = chunkService.GetAllChunkFilesInDirectory(chunksDirectory.FullName);
            var chunkBuffer = new byte[SwarmChunk.SpanAndDataSize];
            
            ioService.WriteLine($"Start uploading {chunkFiles.Length} chunks...");
            
            for (int i = 0; i < chunkFiles.Length; i++)
            {
                if (i % 10000 == 0)
                    ioService.WriteLine($"  {i} chunks uploaded...");

                bool uploadSucceeded = false;
                
                var chunkFilePath = chunkFiles[i];
                var chunkHash = SwarmHash.FromString(Path.GetFileNameWithoutExtension(chunkFilePath));
                using var chunkFileStream = File.OpenRead(chunkFilePath);
                var chunkReadBytes = await chunkFileStream.ReadAsync(chunkBuffer).ConfigureAwait(false);

                var chunk = SwarmChunk.BuildFromSpanAndData(chunkHash, chunkBuffer.AsSpan()[..chunkReadBytes]);
                    
                for (int j = 0; j < UploadMaxRetry && !uploadSucceeded; j++)
                {
                    try
                    {
                        await gatewayService.UploadChunkAsync(
                            batchId,
                            chunk,
                            chunkHash == videoManifestHash && fundPinning);
                    }
                    catch (Exception e)
                    {
                        ioService.WriteErrorLine($"Error uploading chunk {chunkFilePath}");
                        ioService.PrintException(e);
                        if (i + 1 < UploadMaxRetry)
                        {
                            ioService.WriteLine("Retry...");
                            await Task.Delay(UploadRetryTimeSpan);
                        }
                    }
                }
                
                if (!uploadSucceeded)
                    throw new InvalidOperationException($"Can't upload chunk after {UploadMaxRetry} retries");
            }
            ioService.WriteLine($"Chunks upload completed!");
            ioService.WriteLine($"Published with swarm hash (permalink): {video.EthernaPermalinkHash}");
            
            // Fund downloads.
            if (fundDownload)
            {
                await gatewayService.FundResourceDownloadAsync(videoManifestHash);
                ioService.WriteLine($"Funded public download");
            }

            // List on index.
            if (video.EthernaIndexId is null)
                video.EthernaIndexId = await ethernaIndexClient.PublishNewVideoAsync(video.EthernaPermalinkHash!.Value);
            else
                await ethernaIndexClient.UpdateVideoManifestAsync(video.EthernaIndexId, video.EthernaPermalinkHash!.Value);

            ioService.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
        }

        public Task<SwarmHash> UploadVideoManifestAsync(
            VideoManifest videoManifest,
            bool fundPinning,
            bool fundDownload)
        {
            //evaluate to remove this
            throw new NotImplementedException();
        }

        // Helpers.
        private async Task<PostageBatchId> CreatePostageBatchAsync(int batchDepth)
        {
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
            return batchId;
        }
    }
}
