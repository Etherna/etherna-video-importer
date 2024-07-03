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
