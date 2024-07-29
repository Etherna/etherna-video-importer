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
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public abstract class GatewayServiceBase : IGatewayService
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        // Consts.
        protected readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        protected readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);

        // Fields.
        protected readonly IBeeClient beeClient;
        private readonly IIoService ioService;
#pragma warning restore CA1051 // Do not declare visible instance fields

        // Constructor.
        protected GatewayServiceBase(
            IBeeClient beeClient,
            IIoService ioService)
        {
            this.beeClient = beeClient;
            this.ioService = ioService;
        }

        // Methods.
        public abstract Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth);

        public abstract Task DefundResourcePinningAsync(SwarmHash hash);

        public abstract Task FundResourceDownloadAsync(SwarmHash hash);

        public abstract Task<BzzBalance> GetChainPriceAsync();

        public abstract Task<IEnumerable<SwarmHash>> GetPinnedResourcesAsync();

        public abstract Task<bool> IsBatchUsableAsync(PostageBatchId batchId);

        public async Task<SwarmHash> ResolveSwarmAddressToHashAsync(SwarmAddress address) =>
            (await beeClient.ResolveAddressToChunkReferenceAsync(address)).Hash;

        public async Task UploadChunkAsync(PostageBatchId batchId, SwarmChunk chunk, bool fundPinning = false)
        {
            ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));
            
            using var dataStream = new MemoryStream(chunk.Data.ToArray());
            await beeClient.UploadChunkAsync(batchId, dataStream, fundPinning);
        }

        public Task<SwarmHash> UploadFileAsync(
            PostageBatchId batchId,
            Stream content,
            string? name,
            string? contentType,
            bool fundPinning) =>
            beeClient.UploadFileAsync(
                batchId,
                content,
                name: name,
                contentType: contentType,
                swarmPin: fundPinning);

        // Protected methods.
        protected async Task WaitForBatchUsableAsync(PostageBatchId batchId)
        {
            // Wait until created batch is usable.
            ioService.PrintTimeStamp();
            ioService.Write("Waiting for batch being usable... (it may take a while)");

            var batchStartWait = DateTime.UtcNow;
            bool batchIsUsable;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchUsableTimeout)
                {
                    var ex = new InvalidOperationException("Batch not usable after timeout");
                    ex.Data.Add("BatchId", batchId);
                    throw ex;
                }

                batchIsUsable = await IsBatchUsableAsync(batchId);

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            ioService.WriteLine(". Done", false);
        }
    }
}
