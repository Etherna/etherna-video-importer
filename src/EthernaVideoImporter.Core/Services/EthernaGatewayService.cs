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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class EthernaGatewayService : GatewayServiceBase
    {
        // Const.
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);

        // Fields.
        private readonly IEthernaUserGatewayClient ethernaGatewayClient;
        private readonly IIoService ioService;

        // Constructor.
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods")]
        public EthernaGatewayService(
            IEthernaUserGatewayClient ethernaGatewayClient,
            IIoService ioService)
            : base(ethernaGatewayClient.BeeClient, ioService)
        {
            this.ethernaGatewayClient = ethernaGatewayClient;
            this.ioService = ioService;
        }

        // Methods.
        public override async Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth)
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

            await WaitForBatchUsableAsync(batchId.Value);

            return batchId.Value;
        }

        public override Task DefundResourcePinningAsync(SwarmHash hash) =>
            ethernaGatewayClient.DefundResourcePinningAsync(hash);

        public override Task FundResourceDownloadAsync(SwarmHash hash) =>
            ethernaGatewayClient.FundResourceDownloadAsync(hash);

        public override async Task<BzzBalance> GetChainPriceAsync() =>
            (await ethernaGatewayClient.GetChainStateAsync()).CurrentPrice;

        public override async Task<IEnumerable<SwarmHash>> GetPinnedResourcesAsync() =>
            await ethernaGatewayClient.GetPinFundedResourcesAsync();

        public override async Task<bool> IsBatchUsableAsync(PostageBatchId batchId) =>
            (await ethernaGatewayClient.GetPostageBatchAsync(batchId)).IsUsable;
    }
}
