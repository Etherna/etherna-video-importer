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
using Etherna.VideoImporter.Core.Models.Index;
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
    public sealed class VideoService : IVideoService
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
        public VideoService(
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
        public async Task DeleteExogenousVideosAsync(
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            if (indexedVideos is null)
                throw new ArgumentNullException(nameof(indexedVideos));

            Console.WriteLine($"Start removing videos not generated with this tool");

            var exogenousVideos = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName != CommonConsts.ImporterIdentifier);

            foreach (var video in exogenousVideos)
                await RemoveFromIndexAsync(video, gatewayPinnedHashes, unpinRemovedVideos);
        }

        public async Task DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            if (gatewayPinnedHashes is null && unpinRemovedVideos)
                throw new ArgumentNullException(nameof(gatewayPinnedHashes), "gatewayPinnedHashes can't be null if needs to unpin removed video");
            if (indexedVideos is null)
                throw new ArgumentNullException(nameof(indexedVideos));

            Console.WriteLine($"Start removing videos deleted from source");

            var indexedVideosFromImporter = indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.ClientName == CommonConsts.ImporterIdentifier);
            var sourceRemovedVideos = indexedVideos.Where(
                v => !videosMetadataFromSource.Any(metadata => metadata.Id == v.LastValidManifest!.PersonalData!.VideoId));

            foreach (var sourceRemovedVideo in sourceRemovedVideos)
                await RemoveFromIndexAsync(sourceRemovedVideo, gatewayPinnedHashes, unpinRemovedVideos);
        }

        public async Task PinVideoAsync(IndexedVideoManifest indexedVideoManifest, string manifestPermalinkHash)
        {
            if (indexedVideoManifest is null)
                throw new ArgumentNullException(nameof(indexedVideoManifest));

            foreach (var videoResolution in indexedVideoManifest.Sources)
                await ethernaGatewayClient.ResourcesClient.PinPostAsync(videoResolution.Reference);
            if (indexedVideoManifest.Thumbnail?.Sources is not null)
                foreach (var thumbnail in indexedVideoManifest.Thumbnail.Sources)
                    await ethernaGatewayClient.ResourcesClient.PinPostAsync(thumbnail.Value);
            await ethernaGatewayClient.ResourcesClient.PinPostAsync(manifestPermalinkHash);
        }

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

            Console.WriteLine($"Published with swarm hash (permalink): {video.EthernaPermalinkHash}");

            // List on index.

            video.EthernaIndexId = await ethernaIndexClient.VideosClient.VideosPostAsync(
                new VideoCreateInput
                {
                    ManifestHash = video.EthernaPermalinkHash,
                });

            Console.WriteLine($"Listed on etherna index with Id: {video.EthernaIndexId}");
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
                    batchId = await ethernaGatewayClient.SystemClient.PostageBatchRefAsync(batchReferenceId);
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

                batchIsUsable = (await ethernaGatewayClient.UsersClient.BatchesGetAsync(batchId)).Usable;

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            Console.WriteLine(". Done");

            return batchId;
        }

        private async Task RemoveFromIndexAsync(
            IndexedVideo indexedVideo,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos)
        {
            // Remove video.
            bool removeSucceeded = false;
            try
            {
                await ethernaIndexClient.VideosClient.VideosDeleteAsync(indexedVideo.IndexId);
                removeSucceeded = true;

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Video with index Id {indexedVideo} removed");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Unable to remove video with index Id {indexedVideo}");
            }

            Console.ResetColor();

            // Unpin contents.
            if (removeSucceeded &&
                unpinRemovedVideos &&
                indexedVideo.LastValidManifest is not null)
            {
                //videos
                foreach (var streamSource in indexedVideo.LastValidManifest.Sources)
                    await UnpinContentAsync(streamSource.Reference, gatewayPinnedHashes!);

                //thumbnail
                foreach (var thumbSource in indexedVideo.LastValidManifest.Thumbnail.Sources)
                    await UnpinContentAsync(thumbSource.Value, gatewayPinnedHashes!);

                //manifest
                await UnpinContentAsync(indexedVideo.LastValidManifest.Hash, gatewayPinnedHashes!);
            }
        }

        private async Task<bool> UnpinContentAsync(string hash, IEnumerable<string> gatewayPinnedHashes)
        {
            if (!gatewayPinnedHashes.Contains(hash))
                return false;

            try
            {
                await ethernaGatewayClient.ResourcesClient.PinDeleteAsync(hash);

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Resource with hash {hash} unpinned from gateway");

                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Unable to unpin resource with hash {hash} from gateway");

                return false;
            }
        }

    }
}
