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
using Etherna.Sdk.Gateway.GenClients;
using Etherna.Sdk.Users.Gateway.Clients;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class EthernaGatewayService(
        IEthernaUserGatewayClient ethernaGatewayClient,
        IIoService ioService,
        IOptions<GatewayServiceOptions> options)
        : GatewayServiceBase(ethernaGatewayClient.BeeClient, options)
    {
        // Const.
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);

        // Methods.
        public override async Task<BzzBalance> GetChainPriceAsync() =>
            (await ethernaGatewayClient.GetChainStateAsync()).CurrentPrice;

        public override async Task<bool> IsBatchUsableAsync(PostageBatchId batchId) =>
            (await ethernaGatewayClient.GetPostageBatchAsync(batchId)).IsUsable;
        
        // Protected override methods.
        protected override Task AnnounceChunksUploadHelperAsync(SwarmHash rootHash, PostageBatchId batchId) =>
            ethernaGatewayClient.AnnounceChunksUploadAsync(rootHash, batchId);

        protected override async Task<PostageBatchId> CreatePostageBatchHelperAsync(BzzBalance amount, int batchDepth)
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

        protected override Task DefundResourcePinningHelperAsync(SwarmHash hash) =>
            ethernaGatewayClient.DefundResourcePinningAsync(hash);

        protected override Task FundResourceDownloadHelperAsync(SwarmHash hash) =>
            ethernaGatewayClient.FundResourceDownloadAsync(hash);

        protected override Task FundResourcePinningHelperAsync(SwarmHash hash) =>
            ethernaGatewayClient.FundResourcePinningAsync(hash);
    }
}
