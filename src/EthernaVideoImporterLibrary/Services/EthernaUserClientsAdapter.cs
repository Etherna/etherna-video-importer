using Etherna.EthernaVideoImporterLibrary.Models;
using Etherna.ServicesClient;
using Etherna.ServicesClient.Clients.Index;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    public class EthernaUserClientsAdapter : IEthernaUserClientsAdapter
    {
        // Const.
        private const int BLOCK_TIME = 5;
        private const int WAITING_PROPAGATION_BATCH_SECONDS = 5000; // ms.
        private const int WAITING_PROPAGATION_BATCH_RETRY = 50; // retry.

        // Fields.
        private readonly IEthernaUserClients ethernaUserClients;

        // Constructors.
        public EthernaUserClientsAdapter(IEthernaUserClients ethernaUserClients)
        {
            this.ethernaUserClients = ethernaUserClients;
        }

        // Methods.
        public async Task<string> UpsertManifestToIndex(
            string hashReferenceMetadata,
            VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            if (!string.IsNullOrEmpty(videoData.IndexVideoId))
            {
                // Update manifest index.
                Console.WriteLine($"Update Index: {videoData!.IndexVideoId}\t{hashReferenceMetadata}");

                await ethernaUserClients.IndexClient.VideosClient.VideosPutAsync(videoData.IndexVideoId!, hashReferenceMetadata).ConfigureAwait(false);

                return videoData.IndexVideoId!;
            }
            else
            {
                // Create new manifest index.
                Console.WriteLine($"Create Index: {hashReferenceMetadata}");

                var videoCreateInput = new VideoCreateInput
                {
                    ManifestHash = hashReferenceMetadata,
                };
                var indexVideoId = await ethernaUserClients.IndexClient.VideosClient.VideosPostAsync(videoCreateInput).ConfigureAwait(false);

                videoData.SetEthernaIndex(indexVideoId);

                return indexVideoId;
            }
        }

        public async Task<string> CreateBatchAsync(
            VideoData videoData, 
            int ttlPostageStamp)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            // Size of all video to upload.
            var totalSize = videoData.VideoDataResolutions.Sum(v => v.Size);

            // Calculate batch deep.
            var batchDeep = 17;
            while ((2^ batchDeep * 4) < totalSize)
            {
                batchDeep++;
                if (batchDeep > 64)
                    throw new InvalidOperationException("Batch deep exceeds the maximum");
            }

            var chainState = await ethernaUserClients.GatewayClient.SystemClient.ChainstateAsync().ConfigureAwait(false);
            var amount = (long)new TimeSpan(ttlPostageStamp * 24, 0, 0).TotalSeconds * chainState.CurrentPrice / BLOCK_TIME;
            return await ethernaUserClients.GatewayClient.UsersClient.BatchesPostAsync(batchDeep, amount).ConfigureAwait(false);
        }

        public async Task DeleteIndexVideoAsync(string videoId)
        {
            await ethernaUserClients.IndexClient.VideosClient.VideosDeleteAsync(videoId).ConfigureAwait(false);
        }

        public async Task<IEnumerable<VideoDto>> GetAllUserVideoAsync(string userAddress)
        {
            var elements = new List<VideoDto>();
            const int MaxForPage = 100;

            for (var currentPage = 0; true; currentPage++)
            {
                var result = await ethernaUserClients.IndexClient.UsersClient.Videos2Async(userAddress, currentPage, MaxForPage).ConfigureAwait(false);

                if (result?.Elements is null ||
                    !result.Elements.Any())
                    return elements;

                elements.AddRange(result.Elements);
            }
        }

        public async Task<VideoManifestDto?> GetLastValidManifestAsync(string? videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            try
            {
                var videoDto = await ethernaUserClients.IndexClient.VideosClient.VideosGetAsync(videoId).ConfigureAwait(false);
                return videoDto.LastValidManifest;
            }
            catch (IndexApiException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
            catch { throw; }
        }

        public async Task<SystemParametersDto> GetSystemParametersAsync()
        {
            return await ethernaUserClients.IndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);
        }

        public async Task<string> GetBatchIdFromBatchReferenceAsync(string referenceId)
        {
            //Waiting for propagation time on gateway.
            var i = 0;
            while (i < WAITING_PROPAGATION_BATCH_RETRY)
                try
                {
                    i++;
                    return await ethernaUserClients.GatewayClient.SystemClient.PostageBatchRefAsync(referenceId).ConfigureAwait(false);
                }
                catch { await Task.Delay(WAITING_PROPAGATION_BATCH_SECONDS).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get batch id");
        }

        public async Task<bool> IsBatchUsableAsync(string batchId)
        {
            //Waiting for propagation time on bee.
            var i = 0;
            while (i < WAITING_PROPAGATION_BATCH_RETRY)
                try
                {
                    i++;
                    return (await ethernaUserClients.GatewayClient.UsersClient.BatchesGetAsync(batchId).ConfigureAwait(false)).Usable;
                }
                catch { await Task.Delay(WAITING_PROPAGATION_BATCH_SECONDS).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get batch status");
        }

        public async Task OfferResourceAsync(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return;

            await ethernaUserClients.GatewayClient.ResourcesClient.OffersPostAsync(hash).ConfigureAwait(false);
        }
    }
}
