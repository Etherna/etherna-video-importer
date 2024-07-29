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

using Etherna.BeeNet.Models;
using System.Collections.Generic;
using System.IO;
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
        Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth);

        /// <summary>
        /// Delete pin.
        /// </summary>
        /// <param name="hash">Resource hash</param>
        Task DefundResourcePinningAsync(SwarmHash hash);

        /// <summary>
        /// Offer the content to all users.
        /// </summary>
        /// <param name="hash">Resource hash</param>
        Task FundResourceDownloadAsync(SwarmHash hash);

        /// <summary>
        /// Get the current price.
        /// </summary>
        Task<BzzBalance> GetChainPriceAsync();

        /// <summary>
        /// Get all pins.
        /// </summary>
        Task<IEnumerable<SwarmHash>> GetPinnedResourcesAsync();

        /// <summary>
        /// Get usable batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        Task<bool> IsBatchUsableAsync(PostageBatchId batchId);

        Task<SwarmHash> ResolveSwarmAddressToHashAsync(SwarmAddress address);

        Task UploadChunkAsync(
            PostageBatchId batchId,
            SwarmChunk chunk,
            bool fundPinning = false);

        Task<SwarmHash> UploadFileAsync(
            PostageBatchId batchId,
            Stream content,
            string? name,
            string? contentType,
            bool fundPinning);
    }
}
