using Etherna.BeeNet.InputModels;
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

        Task<string> UploadFilesAsync(
            string batchId,
            IEnumerable<FileParameterInput> files,
            bool swarmPin);
    }
}
