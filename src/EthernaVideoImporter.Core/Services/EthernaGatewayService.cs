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
using Etherna.Sdk.Common.GenClients.Gateway;
using Etherna.Sdk.Users.Clients;
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
