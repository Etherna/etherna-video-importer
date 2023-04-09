using Etherna.BeeNet.InputModels;
using Etherna.VideoImporter.Core.Models.SwarmDtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IGatewayService
    {
        /// <summary>
        /// Create a new batch.
        /// </summary>
        /// <param name="amount">amount</param>
        /// <param name="batchDepth">batch depth</param>
        Task<string> CreatePostageBatchAsync(long amount, int batchDepth);

        /// <summary>
        /// Delete pin.
        /// </summary>
        /// <param name="hash">Video data</param>
        Task DeletePinAsync(string hash);

        /// <summary>
        /// Dilute a batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        /// <param name="batchDepth">batch depth</param>
        Task DilutePostageBatchAsync(string batchId, int batchDepth);

        /// <summary>
        /// Get stats batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        Task<PostageBatchDto> GetBatchStatsAsync(string batchId);

        /// <summary>
        /// Get the current price.
        /// </summary>
        Task<long> GetCurrentChainPriceAsync();

        /// <summary>
        /// Get usable batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        Task<bool> IsBatchUsableAsync(string batchId);

        /// <summary>
        /// Offer the content to all users.
        /// </summary>
        /// <param name="hash">hash</param>
        Task OfferContentAsync(string hash);

        /// <summary>
        /// Get all pins.
        /// </summary>
        Task<IEnumerable<string>> GetPinnedResourcesAsync();

        /// <summary>
        /// Upload files.
        /// </summary>
        /// <param name="batchId">batch id</param>
        /// <param name="files">files to upoad</param>
        /// <param name="swarmPin">pin file if true</param>
        Task<string> UploadFilesAsync(
            string batchId,
            IEnumerable<FileParameterInput> files,
            bool swarmPin);
    }
}
