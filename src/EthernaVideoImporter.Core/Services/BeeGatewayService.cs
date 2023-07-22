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

        public override Task OfferContentAsync(string hash) =>
            ethernaUserGatewayClient.ResourcesClient.OffersPostAsync(hash);
    }
}
