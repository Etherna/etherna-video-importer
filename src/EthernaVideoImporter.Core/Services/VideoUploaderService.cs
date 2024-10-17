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

using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Etherna.Sdk.Tools.Video.Models;
using Etherna.Sdk.Tools.Video.Services;
using Etherna.Sdk.Users.Index.Clients;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Domain.Directories;
using Etherna.VideoImporter.Core.Options;
using Etherna.VideoImporter.Core.Utilities;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class VideoUploaderService(
        IAppVersionService appVersionService,
        IChunkService chunkService,
        IEthernaUserIndexClient ethernaIndexClient,
        IGatewayService gatewayService,
        IHasher hasher,
        IIoService ioService,
        IOptions<VideoUploaderServiceOptions> options,
        IVideoManifestService videoManifestService)
        : IVideoUploaderService
    {
        // Const.
        private const int BeeMaxRetry = 10;
        private readonly TimeSpan UploadRetryTimeSpan = TimeSpan.FromSeconds(5);

        // Fields.
        private readonly VideoUploaderServiceOptions options = options.Value;

        // Public methods.
        public async Task UploadVideoAsync(
            Video video,
            bool fundPinning,
            bool fundDownload,
            string ownerEthAddress,
            ProjectDirectory projectDirectory,
            PostageBatchId? batchId = null)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));
            
            // Create chunks. Do as first thing, also to evaluate required postage batch depth.
            var chunksDirectory = projectDirectory.ChunksDirectory.CreateDirectory();
            var stampIssuer = new PostageStampIssuer(PostageBatch.MaxDepthInstance);
            
            //video source files, exclude already uploaded Swarm files
            if (video.VideoEncoding.MasterFile is { SwarmHash: null })
            {
                ioService.WriteLine($"Creating chunks of master playlist in progress...");

                using var stream = await video.VideoEncoding.MasterFile.ReadToStreamAsync();
                video.VideoEncoding.MasterFile.SwarmHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
            }
            foreach (var variant in video.VideoEncoding.Variants.Where(v => v.EntryFile.SwarmHash is null))
            {
                ioService.WriteLine($"Creating chunks of {variant.QualityLabel} video variant in progress...");
                
                //common entry file
                using var stream = await variant.EntryFile.ReadToStreamAsync();
                variant.EntryFile.SwarmHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
                
                //additional files.
                switch (variant)
                {
                    case HlsVideoVariant hlsVariant:
                        foreach (var segment in hlsVariant.HlsSegmentFiles)
                        {
                            using var segStream = await segment.ReadToStreamAsync();
                            segment.SwarmHash = await chunkService.WriteDataChunksAsync(
                                segStream,
                                chunksDirectory.FullName,
                                postageStampIssuer: stampIssuer);
                        }
                        break;
                    case SingleFileVideoVariant: break;
                    default: throw new InvalidOperationException();
                }
            }
            
            //thumbnail source files, exclude already uploaded Swarm files 
            ioService.WriteLine($"Creating chunks of thumbnail in progress...");
            foreach (var thumbnailFile in video.ThumbnailFiles.Where(f => f.SwarmHash is null))
            {
                using var stream = await thumbnailFile.ReadToStreamAsync();
                thumbnailFile.SwarmHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
            }
            
            //subtitle source files, exclude already uploaded Swarm files 
            ioService.WriteLine($"Creating chunks of subtitles in progress...");
            foreach (var subtitleFile in video.SubtitleFiles.Where(f => f.SwarmHash is null))
            {
                using var stream = await subtitleFile.ReadToStreamAsync();
                subtitleFile.SwarmHash = await chunkService.WriteDataChunksAsync(
                    stream,
                    chunksDirectory.FullName,
                    postageStampIssuer: stampIssuer);
            }
            
            //new video manifest (at first without batchId. See: https://etherna.atlassian.net/browse/EVMS-8).
            //personal data
            var personalData = new VideoManifestPersonalData(
                CommonConsts.ImporterIdentifier,
                appVersionService.CurrentVersion.ToString(),
                video.Metadata.SourceName,
                hasher.ComputeHash(video.Metadata.SourceId).ToHex());
            
            //video manifest video sources
            var manifestVideoSources = video.VideoEncoding.Variants.Select(v =>
            {
                var sourceRelativePath = v.EntryFile.UUri.OriginalUri;
                if (video.VideoEncoding.EncodingDirectoryPath != null)
                    sourceRelativePath = Path.GetRelativePath(video.VideoEncoding.EncodingDirectoryPath, sourceRelativePath);
                    
                return new VideoManifestVideoSource(
                    sourceRelativePath,
                    video.VideoEncoding switch
                    {
                        Mp4VideoEncoding _ => VideoType.Mp4,
                        HlsVideoEncoding _ => VideoType.Hls,
                        _ => throw new InvalidOperationException()
                    },
                    v.QualityLabel,
                    v.TotalByteSize,
                    v switch
                    {
                        HlsVideoVariant hlsV => hlsV.HlsSegmentFiles.Select(segment =>
                            {
                                var segmentRelativePath = segment.UUri.OriginalUri;
                                if (video.VideoEncoding.EncodingDirectoryPath != null)
                                    segmentRelativePath = Path.GetRelativePath(video.VideoEncoding.EncodingDirectoryPath, segmentRelativePath);
                                
                                return new VideoManifestVideoSourceAdditionalFile(
                                    segmentRelativePath,
                                    segment.SwarmHash ??
                                    throw new InvalidOperationException("Swarm hash can't be null here"));
                            })
                            .ToArray(),
                        _ => []
                    },
                    v.EntryFile.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here"));
            });
            if (video.VideoEncoding.MasterFile != null)
            {
                var masterFile = video.VideoEncoding.MasterFile;
                manifestVideoSources = manifestVideoSources.Prepend(new VideoManifestVideoSource(
                    masterFile.FileName,
                    video.VideoEncoding switch
                    {
                        HlsVideoEncoding _ => VideoType.Hls,
                        _ => throw new InvalidOperationException()
                    },
                    null,
                    0, //need to be 0 with manifest v2, to be recognizable
                    [],
                    masterFile.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here")));
            }
            
            //video manifest thumbnail
            var manifestThumbnail = new VideoManifestImage(
                video.AspectRatio,
                video.ThumbnailBlurhash,
                video.ThumbnailFiles.Select(t =>
                    new VideoManifestImageSource(
                    t.FileName,
                    t.ImageType,
                    t.Width,
                    t.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here"))));
            
            //video manifest captions
            var manifestSubtitleSources = video.SubtitleFiles.Select(s =>
                new VideoManifestCaptionSource(
                    s.Label,
                    s.LanguageCode,
                    s.FileName,
                    s.SwarmHash ?? throw new InvalidOperationException("Swarm hash can't be null here")));
            
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
                manifestThumbnail,
                manifestSubtitleSources);

            await videoManifestService.CreateVideoManifestChunksAsync(
                videoManifest,
                chunksDirectory.FullName,
                postageStampIssuer: stampIssuer);
            
            // Create new batch if required.
            batchId ??= await CreatePostageBatchAsync(stampIssuer.Buckets.RequiredPostageBatchDepth);
            
            // Assign batchId to manifest, and re-create manifest chunks. Get final hash.
            videoManifest.BatchId = batchId.Value;
            var videoManifestHash = await videoManifestService.CreateVideoManifestChunksAsync(
                videoManifest,
                chunksDirectory.FullName,
                postageStampIssuer: stampIssuer);
            
            video.EthernaPermalinkHash = videoManifestHash;
            
            // Upload chunks. Pin only video manifest hash, if required.
            var chunkFiles = chunkService.GetAllChunkFilesInDirectory(chunksDirectory.FullName);
            
            ioService.WriteLine($"Start uploading {chunkFiles.Length} chunks...");
            
            // //required to not bypass bee local storage
            // var tagInfo = await gatewayService.CreateTagAsync(videoManifestHash, batchId.Value);
            
            // // Required to pre-pin the root chunk on same node owning the postage batch.
            // // Doesn't actual pin the content. It's needed to pre-allocate the right node
            // // for the successive pinning on gateway db.
            // await gatewayService.AnnounceChunksUploadAsync(videoManifestHash, batchId.Value);
            
            int totalUploaded = 0;
            var uploadStartDateTime = DateTime.UtcNow;
            for (int retry = 0; retry < BeeMaxRetry && totalUploaded < chunkFiles.Length; retry++)
            {
                // Skip with dry run.
                if (options.IsDryRun)
                    break;

                try
                {
                    var lastUpdateDateTime = DateTime.UtcNow;
                    
                    while (totalUploaded < chunkFiles.Length)
                    {
                        var now = DateTime.UtcNow;
                        if (now - lastUpdateDateTime > TimeSpan.FromSeconds(1))
                        {
                            PrintProgressLine(
                                "Uploading chunks",
                                totalUploaded,
                                chunkFiles.Length,
                                uploadStartDateTime);
                            lastUpdateDateTime = now;
                        }
                        
                        var chunkBatchFiles = chunkFiles.Skip(totalUploaded).Take(GatewayService.ChunkBatchMaxSize).ToArray();

                        List<SwarmChunk> chunkBatch = [];
                        foreach (var chunkPath in chunkBatchFiles)
                        {
                            chunkBatch.Add(SwarmChunk.BuildFromSpanAndData(
                                Path.GetFileNameWithoutExtension(chunkPath),
                                await File.ReadAllBytesAsync(chunkPath)));
                        }

                        await gatewayService.ChunksBulkUploadAsync(
                            chunkBatch.ToArray(),
                            batchId.Value);
                        retry = 0;
                        
                        totalUploaded += chunkBatchFiles.Length;
                    }
                    ioService.WriteLine(null, false);
                }
                catch (Exception e) when (
                    e is InvalidOperationException
                        or WebSocketException
                        or OperationCanceledException)
                {
                    ioService.WriteErrorLine($"Error uploading chunks");
                    ioService.PrintException(e);

                    if (retry + 1 < BeeMaxRetry)
                    {
                        Console.WriteLine("Retry...");
                        await Task.Delay(UploadRetryTimeSpan);
                    }
                }
            }
            
            // Fund pinning.
            // if (fundPinning)
            // {
            //     for (int i = 0; i < BeeMaxRetry; i++)
            //     {
            //         try
            //         {
            //             await gatewayService.FundResourcePinningAsync(videoManifestHash);
            //             ioService.WriteLine("Funded video pinning");
            //             
            //             break;
            //         }
            //         catch (Exception e)
            //         {
            //             ioService.WriteError("Error funding video pinning");
            //             ioService.PrintException(e);
            //
            //             if (i + 1 < BeeMaxRetry)
            //             {
            //                 Console.WriteLine("Retry...");
            //                 await Task.Delay(UploadRetryTimeSpan);
            //             }
            //         }
            //     }
            // }
            
            // // Update tag info and delete it to indicate end of upload.
            // await gatewayService.UpdateTagInfoAsync(tagInfo.Id, videoManifestHash, batchId.Value);
            // await gatewayService.DeleteTagAsync(tagInfo.Id, batchId.Value);
            
            // Fund downloads.
            if (fundDownload)
            {
                try
                {
                    await gatewayService.FundResourceDownloadAsync(videoManifestHash);
                    ioService.WriteLine("Funded public download");
                }
                catch (Exception e)
                {
                    ioService.WriteError("Error funding public download");
                    ioService.PrintException(e);
                }
            }
            
            // Upload completed.
            ioService.WriteLine($"Chunks upload completed!");
            ioService.WriteLine($"Published on (permalink): {UrlBuilder.BuildNormalPermalinkUrl(video.EthernaPermalinkHash.Value)}");

            // List on index.
            if (!options.IsDryRun)
            {
                if (video.EthernaIndexId is null)
                    video.EthernaIndexId = await ethernaIndexClient.PublishNewVideoAsync(video.EthernaPermalinkHash!.Value);
                else
                    await ethernaIndexClient.UpdateVideoManifestAsync(video.EthernaIndexId, video.EthernaPermalinkHash!.Value);
            }
            else
            {
                video.EthernaIndexId = "0000000";
            }

            ioService.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
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

        private void PrintProgressLine(string message, long uploadedChunks, long totalChunks, DateTime startDateTime)
        {
            // Calculate ETA.
            var elapsedTime = DateTime.UtcNow - startDateTime;
            TimeSpan? eta = null;
            var progressStatus = (double)uploadedChunks / totalChunks;
            if (progressStatus != 0)
            {
                var totalRequiredTime = TimeSpan.FromSeconds(elapsedTime.TotalSeconds / progressStatus);
                eta = totalRequiredTime - elapsedTime;
            }

            // Print update.
            var strBuilder = new StringBuilder();

            strBuilder.Append(CultureInfo.InvariantCulture,
                $"{message} ({(progressStatus * 100):N2}%) {uploadedChunks} chunks of {totalChunks}.");

            if (eta is not null)
                strBuilder.Append(CultureInfo.InvariantCulture, $" ETA: {eta:hh\\:mm\\:ss}");

            strBuilder.Append('\r');

            ioService.Write(strBuilder.ToString());
        }
    }
}
