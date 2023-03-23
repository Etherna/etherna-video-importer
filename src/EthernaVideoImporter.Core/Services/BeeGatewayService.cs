using Etherna.BeeNet.Clients.GatewayApi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class BeeGatewayService : IGatewayService
    {
        // Fields.
        private readonly IBeeGatewayClient beeGatewayClient;

        // Constructor.
        public BeeGatewayService(IBeeGatewayClient beeGatewayClient)
        {
            this.beeGatewayClient = beeGatewayClient;
        }

        // Methods.
        public async Task<long> ChainCurrentPriceAsync()
        {
            var chainState = await beeGatewayClient.GetChainStateAsync();
            
            return chainState.CurrentPrice;
        }

        public async Task<string> CreatePostageBatchAsync(int batchDeep, long amount)
        {
            return await beeGatewayClient.BuyPostageBatchAsync(amount, batchDeep); //Label or other fields to send?
        }

        public Task OffersPostAsync(string hash)
        {
            throw new NotImplementedException();
        }

        public async Task PinDeleteAsync(string hash)
        {
            await beeGatewayClient.DeletePinAsync(hash);
        }

        public async Task<IEnumerable<string>> PinnedResourcesAsync()
        {
            return await beeGatewayClient.GetAllPinsAsync();
        }
    }
}
