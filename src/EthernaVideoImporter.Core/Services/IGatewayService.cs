// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
        /// <param name="address">Resource address</param>
        Task DefundResourcePinningAsync(SwarmAddress address);

        /// <summary>
        /// Get the current price.
        /// </summary>
        Task<BzzBalance> GetChainPriceAsync();

        /// <summary>
        /// Get usable batch.
        /// </summary>
        /// <param name="batchId">batch id</param>
        Task<bool> IsBatchUsableAsync(PostageBatchId batchId);

        /// <summary>
        /// Offer the content to all users.
        /// </summary>
        /// <param name="address">Resource address</param>
        Task FundResourceDownloadAsync(SwarmAddress address);

        /// <summary>
        /// Get all pins.
        /// </summary>
        Task<IEnumerable<SwarmAddress>> GetPinnedResourcesAsync();

        Task<SwarmAddress> UploadFileAsync(
            PostageBatchId batchId,
            Stream content,
            string? name,
            string? contentType,
            bool swarmPin);
    }
}
