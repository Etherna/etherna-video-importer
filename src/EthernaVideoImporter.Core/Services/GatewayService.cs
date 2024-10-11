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
using Etherna.BeeNet.Tools;
using Etherna.Sdk.Gateway.GenClients;
using Etherna.Sdk.Users.Gateway.Clients;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public abstract class GatewayService(
        IEthernaUserGatewayClient ethernaGatewayClient,
        IIoService ioService,
        IOptions<GatewayServiceOptions> options)
        : IGatewayService
    {
        // Consts.
        private static readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        private static readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);
        private static readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);
        private const ushort ChunkBatchMaxSize = 1000;

        // Fields.
        private readonly GatewayServiceOptions options = options.Value;

        // Methods.
        public Task AnnounceChunksUploadAsync(SwarmHash rootHash, PostageBatchId batchId)
        {
            if (options.IsDryRun)
                return Task.CompletedTask;
            if (options.UseBeeApi)
                return Task.CompletedTask; //not required because it's a single node, but no reason to fail
            return ethernaGatewayClient.AnnounceChunksUploadAsync(rootHash, batchId);
        }

        public async Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth)
        {
            if (options.IsDryRun)
                return PostageBatchId.Zero;
            if (options.UseBeeApi)
            {
                ioService.PrintTimeStamp();
                ioService.Write("Waiting for batch created... (it may take a while)");
            
                var batchId = await ethernaGatewayClient.BeeClient.BuyPostageBatchAsync(amount, batchDepth);
            
                ioService.WriteLine(". Done", false);

                // Wait until created batch is usable.
                ioService.PrintTimeStamp();
                ioService.Write("Waiting for batch being usable... (it may take a while)");
            
                await WaitForBatchUsableAsync(batchId);

                ioService.WriteLine(". Done", false);

                return batchId;
            }
            else
            {
                var batchReferenceId = await ethernaGatewayClient.BuyPostageBatchAsync(amount, batchDepth);

                // Wait until created batch is available.
                ioService.PrintTimeStamp();
                ioService.Write("Waiting for batch created... (it may take a while)");

                var batchStartWait = DateTime.UtcNow;
                PostageBatchId? batchId = null;
                do
                {
                    //timeout throw exception
                    if (DateTime.UtcNow - batchStartWait >= BatchCreationTimeout)
                    {
                        var ex = new InvalidOperationException("Batch not available after timeout");
                        ex.Data.Add("BatchReferenceId", batchReferenceId);
                        throw ex;
                    }

                    try
                    {
                        batchId = await ethernaGatewayClient.TryGetNewPostageBatchIdFromPostageRefAsync(batchReferenceId);
                    }
                    catch (EthernaGatewayApiException)
                    {
                        //waiting for batchId available
                        await Task.Delay(BatchCheckTimeSpan);
                    }
                } while (batchId is null);

                ioService.WriteLine(". Done", false);

                // Wait until created batch is usable.
                ioService.PrintTimeStamp();
                ioService.Write("Waiting for batch being usable... (it may take a while)");

                await WaitForBatchUsableAsync(batchId.Value);

                ioService.WriteLine(". Done", false);

                return batchId.Value;
            }
        }

        public Task<TagInfo> CreateTagAsync(PostageBatchId postageBatchId)
        {
            if (options.IsDryRun)
                return Task.FromResult(new TagInfo(new TagId(0), DateTimeOffset.UtcNow, 0, 0, 0, 0, 0));
            return ethernaGatewayClient.BeeClient.CreateTagAsync(postageBatchId);
        }

        public Task DefundResourcePinningAsync(SwarmHash hash)
        {
            if (options.IsDryRun)
                return Task.CompletedTask;
            if (options.UseBeeApi)
                return ethernaGatewayClient.BeeClient.DeletePinAsync(hash);
            return ethernaGatewayClient.DefundResourcePinningAsync(hash);
        }

        public Task FundResourceDownloadAsync(SwarmHash hash)
        {
            if (options.IsDryRun)
                return Task.CompletedTask;
            if (options.UseBeeApi)
                throw new NotSupportedException();
            return ethernaGatewayClient.FundResourceDownloadAsync(hash);
        }

        public Task FundResourcePinningAsync(SwarmHash hash)
        {
            if (options.IsDryRun)
                return Task.CompletedTask;
            if (options.UseBeeApi)
                return ethernaGatewayClient.BeeClient.CreatePinAsync(hash);
            return ethernaGatewayClient.FundResourcePinningAsync(hash);
        }

        public async Task<BzzBalance> GetChainPriceAsync()
        {
            if (options.UseBeeApi)
                return (await ethernaGatewayClient.BeeClient.GetChainStateAsync()).CurrentPrice;
            return (await ethernaGatewayClient.GetChainStateAsync()).CurrentPrice;
        }

        public Task<IChunkWebSocketUploader> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default)
        {
            if (options.UseBeeApi)
                ethernaGatewayClient.BeeClient.GetChunkUploaderWebSocketAsync(batchId, tagId, cancellationToken);
            return ethernaGatewayClient.GetChunkTurboUploaderWebSocketAsync(batchId, tagId, ChunkBatchMaxSize, cancellationToken);
        }

        public async Task<bool> IsBatchUsableAsync(PostageBatchId batchId)
        {
            if (options.UseBeeApi)
                return (await ethernaGatewayClient.BeeClient.GetPostageBatchAsync(batchId)).IsUsable;
            return (await ethernaGatewayClient.GetPostageBatchAsync(batchId)).IsUsable;
        }

        public async Task<SwarmHash> ResolveSwarmAddressToHashAsync(SwarmAddress address) =>
            (await ethernaGatewayClient.BeeClient.ResolveAddressToChunkReferenceAsync(address)).Hash;

        public async Task UploadChunkAsync(PostageBatchId batchId, SwarmChunk chunk, bool fundPinning = false)
        {
            ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));

            if (options.IsDryRun)
                return;
            
            using var dataStream = new MemoryStream(chunk.Data.ToArray());
            await ethernaGatewayClient.BeeClient.UploadChunkAsync(batchId, dataStream, fundPinning);
        }

        // Protected methods.
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
