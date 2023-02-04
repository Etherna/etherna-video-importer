using Etherna.EthernaVideoImporterLibrary.Models;
using Etherna.ServicesClient.Clients.Index;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    /// <summary>
    /// Etherna services
    /// </summary>
    public interface IEthernaUserClientsAdapter
    {
        /// <summary>
        /// Create batch
        /// </summary>
        Task<string> CreateBatchAsync();

        /// <summary>
        /// Delete video from index
        /// </summary>
        /// <param name="videoId">Video id</param>
        Task DeleteIndexVideoAsync(string videoId);

        /// <summary>
        /// Get last vaid manifest
        /// </summary>
        /// <param name="videoId">Video id</param>
        Task<VideoManifestDto?> GetLastValidManifestAsync(string? videoId);

        /// <summary>
        /// Get indexer info
        /// </summary>
        Task<SystemParametersDto> GetSystemParametersAsync();

        /// <summary>
        /// Get all user video metadata
        /// </summary>
        Task<IEnumerable<VideoDto>> GetAllUserVideoAsync(string userAddress);

        /// <summary>
        /// Get batch id from reference
        /// </summary>
        /// <param name="referenceId">Reference id</param>
        Task<string> GetBatchIdFromBatchReferenceAsync(string referenceId);

        /// <summary>
        /// Get if batch usable
        /// </summary>
        /// <param name="batchId">Batch id</param>
        Task<bool> IsBatchUsableAsync(string batchId);

        /// <summary>
        /// Set video offer by creator
        /// </summary>
        /// <param name="hash">hash</param>
        Task OfferResourceAsync(string hash);

        /// <summary>
        /// Update manifest index (or create if not exist)
        /// </summary>
        /// <param name="hash">hash</param>
        Task<string> UpsertManifestToIndex(
            string hashReferenceMetadata,
            VideoData videoData);
    }
}
