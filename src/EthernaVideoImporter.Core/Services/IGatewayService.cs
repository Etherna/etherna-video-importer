using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IGatewayService
    {
        /// <summary>
        /// Get usable batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        Task<bool> BatchIsUsableAsync(string batchId);

        /// <summary>
        /// Get the current price.
        /// </summary>
        Task<long> ChainCurrentPriceAsync();

        /// <summary>
        /// Create a new batch.
        /// </summary>
        /// <param name="batchDeep">batch deep</param>
        /// <param name="amount">amount</param>
        Task<string> CreatePostageBatchAsync(int batchDeep, long amount);

        /// <summary>
        /// Offer the content to all users.
        /// </summary>
        /// <param name="hash">hash</param>
        Task OffersPostAsync(string hash);

        /// <summary>
        /// Delete pin.
        /// </summary>
        /// <param name="hash">Video data</param>
        Task PinDeleteAsync(string hash);

        /// <summary>
        /// Get all pins.
        /// </summary>
        Task<IEnumerable<string>> PinnedResourcesAsync();
    }
}
