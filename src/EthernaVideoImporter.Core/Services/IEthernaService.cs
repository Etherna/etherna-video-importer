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

using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Etherna services
    /// </summary>
    public interface IEthernaUserClientsAdapter
    {
        /// <summary>
        /// Create batch
        /// </summary>
        Task<string> CreateBatchAsync(
            VideoData videoData,
            int ttlPostageStamp);

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
