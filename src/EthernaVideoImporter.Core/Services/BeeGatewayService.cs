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
using Etherna.Sdk.Users.Gateway.Clients;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class BeeGatewayService : GatewayServiceBase
    {
        // Fields.
        private readonly IEthernaUserGatewayClient ethernaGatewayClient;
        private readonly IIoService ioService;

        // Constructor.
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods")]
        public BeeGatewayService(
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
            ioService.PrintTimeStamp();
            ioService.Write("Waiting for batch created... (it may take a while)");
            var batchId = await beeClient.BuyPostageBatchAsync(amount, batchDepth);
            ioService.WriteLine(". Done", false);

            await WaitForBatchUsableAsync(batchId);

            return batchId;
        }

        public override Task DefundResourcePinningAsync(SwarmHash hash) =>
            beeClient.DeletePinAsync(hash);

        public override async Task FundResourceDownloadAsync(SwarmHash hash) =>
            await ethernaGatewayClient.FundResourceDownloadAsync(hash);

        public override async Task<BzzBalance> GetChainPriceAsync() =>
             (await beeClient.GetChainStateAsync()).CurrentPrice;

        public override Task<IEnumerable<SwarmHash>> GetPinnedResourcesAsync() =>
            beeClient.GetAllPinsAsync();

        public override async Task<bool> IsBatchUsableAsync(PostageBatchId batchId) =>
            (await beeClient.GetPostageBatchAsync(batchId)).IsUsable;
    }
}
