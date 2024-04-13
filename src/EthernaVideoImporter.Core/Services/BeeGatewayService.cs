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

using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.ServicesClient.Users;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class BeeGatewayService : GatewayServiceBase
    {
        // Fields.
        private readonly IEthernaUserGatewayClient ethernaUserGatewayClient;

        // Constructor.
        public BeeGatewayService(
            IBeeGatewayClient beeGatewayClient,
            IEthernaUserGatewayClient ethernaUserGatewayClient)
            : base(beeGatewayClient)
        {
            this.ethernaUserGatewayClient = ethernaUserGatewayClient;
        }

        // Methods.
        public override async Task<string> CreatePostageBatchAsync(long amount, int batchDepth)
        {
            Console.Write("Waiting for batch created... (it may take a while)");
            var batchId = await beeGatewayClient.BuyPostageBatchAsync(amount, batchDepth);
            Console.WriteLine(". Done");

            await WaitForBatchUsableAsync(batchId);

            return batchId;
        }

        public override Task DeletePinAsync(string hash) =>
            beeGatewayClient.DeletePinAsync(hash);

        public override async Task<long> GetCurrentChainPriceAsync() =>
             (await beeGatewayClient.GetChainStateAsync()).CurrentPrice;

        public override Task<IEnumerable<string>> GetPinnedResourcesAsync() =>
            beeGatewayClient.GetAllPinsAsync();

        public override async Task<bool> IsBatchUsableAsync(string batchId) =>
            (await beeGatewayClient.GetPostageBatchAsync(batchId)).Usable;

        public override async Task OfferContentAsync(string hash) =>
            await ethernaUserGatewayClient.ResourcesClient.OffersPostAsync(hash);
    }
}
