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

using Etherna.BeeNet;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Tools;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IGatewayService
    {
        // Properties.
        IBeeClient BeeClient { get; }
        
        // Methods.
        /// <summary>
        /// Create a new batch.
        /// </summary>
        /// <param name="amount">amount</param>
        /// <param name="batchDepth">batch depth</param>
        Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth);

        Task<TagInfo> CreateTagAsync(PostageBatchId postageBatchId);

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

        Task FundResourcePinningAsync(SwarmHash hash);

        /// <summary>
        /// Get the current price.
        /// </summary>
        Task<BzzBalance> GetChainPriceAsync();

        Task<IChunkWebSocketUploader> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default);

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

        Task AnnounceChunksUploadAsync(SwarmHash rootHash, PostageBatchId batchId);
    }
}
