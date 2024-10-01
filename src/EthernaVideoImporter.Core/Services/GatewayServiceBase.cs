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
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public abstract class GatewayServiceBase(
        IBeeClient beeClient,
        IOptions<GatewayServiceOptions> options)
        : IGatewayService
    {
        // Consts.
#pragma warning disable CA1051 // Do not declare visible instance fields
        protected readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        protected readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);
#pragma warning restore CA1051 // Do not declare visible instance fields

        // Fields.
        private readonly GatewayServiceOptions options = options.Value;

        // Properties.
        public IBeeClient BeeClient { get; } = beeClient;

        // Methods.
        public Task AnnounceChunksUploadAsync(SwarmHash rootHash, PostageBatchId batchId) => options.IsDryRun ?
            Task.CompletedTask :
            AnnounceChunksUploadHelperAsync(rootHash, batchId);

        public Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth) => options.IsDryRun ?
            Task.FromResult(PostageBatchId.Zero) :
            CreatePostageBatchHelperAsync(amount, batchDepth);

        public Task<TagInfo> CreateTagAsync(PostageBatchId postageBatchId) => options.IsDryRun ?
            Task.FromResult(new TagInfo(new TagId(0), DateTimeOffset.UtcNow, 0, 0, 0, 0, 0)) :
            BeeClient.CreateTagAsync(postageBatchId);

        public Task DefundResourcePinningAsync(SwarmHash hash) => options.IsDryRun ?
            Task.CompletedTask :
            DefundResourcePinningHelperAsync(hash);

        public Task FundResourceDownloadAsync(SwarmHash hash) => options.IsDryRun ?
            Task.CompletedTask :
            FundResourceDownloadHelperAsync(hash);

        public Task FundResourcePinningAsync(SwarmHash hash) => options.IsDryRun ?
            Task.CompletedTask :
            FundResourcePinningHelperAsync(hash);

        public abstract Task<BzzBalance> GetChainPriceAsync();

        public Task<ChunkUploaderWebSocket> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default) =>
            BeeClient.GetChunkUploaderWebSocketAsync(batchId, tagId, cancellationToken);

        public abstract Task<bool> IsBatchUsableAsync(PostageBatchId batchId);

        public async Task<SwarmHash> ResolveSwarmAddressToHashAsync(SwarmAddress address) =>
            (await BeeClient.ResolveAddressToChunkReferenceAsync(address)).Hash;

        public async Task UploadChunkAsync(PostageBatchId batchId, SwarmChunk chunk, bool fundPinning = false)
        {
            ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));

            if (options.IsDryRun)
                return;
            
            using var dataStream = new MemoryStream(chunk.Data.ToArray());
            await BeeClient.UploadChunkAsync(batchId, dataStream, fundPinning);
        }

        // Protected methods.
        protected abstract Task AnnounceChunksUploadHelperAsync(SwarmHash rootHash, PostageBatchId batchId);
        
        protected abstract Task<PostageBatchId> CreatePostageBatchHelperAsync(BzzBalance amount, int batchDepth);
        
        protected abstract Task DefundResourcePinningHelperAsync(SwarmHash hash);
        
        protected abstract Task FundResourceDownloadHelperAsync(SwarmHash hash);
        
        protected abstract Task FundResourcePinningHelperAsync(SwarmHash hash);
        
        protected async Task WaitForBatchUsableAsync(PostageBatchId batchId)
        {
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
        }
    }
}
