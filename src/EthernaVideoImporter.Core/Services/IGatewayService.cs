﻿// Copyright 2022-present Etherna SA
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

        Task<string> UploadFileAsync(
            string batchId,
            Stream content,
            string? name,
            string? contentType,
            bool swarmPin);
    }
}
