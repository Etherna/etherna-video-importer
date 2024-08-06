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
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class BeeGatewayService(
        IEthernaUserGatewayClient ethernaGatewayClient,
        IIoService ioService,
        IOptions<GatewayServiceOptions> options)
        : GatewayServiceBase(ethernaGatewayClient.BeeClient, options)
    {
        // Methods.
        public override async Task<BzzBalance> GetChainPriceAsync() =>
             (await BeeClient.GetChainStateAsync()).CurrentPrice;

        public override async Task<bool> IsBatchUsableAsync(PostageBatchId batchId) =>
            (await BeeClient.GetPostageBatchAsync(batchId)).IsUsable;
        
        // Protected override methods.
        protected override async Task<PostageBatchId> CreatePostageBatchHelperAsync(BzzBalance amount, int batchDepth)
        {
            ioService.PrintTimeStamp();
            ioService.Write("Waiting for batch created... (it may take a while)");
            
            var batchId = await BeeClient.BuyPostageBatchAsync(amount, batchDepth);
            
            ioService.WriteLine(". Done", false);

            // Wait until created batch is usable.
            ioService.PrintTimeStamp();
            ioService.Write("Waiting for batch being usable... (it may take a while)");
            
            await WaitForBatchUsableAsync(batchId);

            ioService.WriteLine(". Done", false);

            return batchId;
        }

        protected override Task DefundResourcePinningHelperAsync(SwarmHash hash) =>
            BeeClient.DeletePinAsync(hash);

        protected override async Task FundResourceDownloadHelperAsync(SwarmHash hash) =>
            await ethernaGatewayClient.FundResourceDownloadAsync(hash);
    }
}
