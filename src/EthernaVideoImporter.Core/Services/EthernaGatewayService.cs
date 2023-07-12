using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.ServicesClient.GeneratedClients.Gateway;
using Etherna.ServicesClient.Users;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class EthernaGatewayService : GatewayServiceBase
    {
        // Const.
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);

        // Fields.
        private readonly IEthernaUserGatewayClient ethernaGatewayClient;

        // Constructor.
        public EthernaGatewayService(
            IBeeGatewayClient beeGatewayClient,
            IEthernaUserGatewayClient ethernaGatewayClient)
            : base(beeGatewayClient)
        {
            this.ethernaGatewayClient = ethernaGatewayClient;
        }

        // Methods.
        public override async Task<string> CreatePostageBatchAsync(long amount, int batchDepth)
        {
            var batchReferenceId = await ethernaGatewayClient.UsersClient.BatchesPostAsync(batchDepth, amount);

            // Wait until created batch is avaiable.
            Console.Write("Waiting for batch created... (it may take a while)");

            var batchStartWait = DateTime.UtcNow;
            string? batchId = null;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchCreationTimeout)
                {
                    var ex = new InvalidOperationException("Batch not avaiable after timeout");
                    ex.Data.Add("BatchReferenceId", batchReferenceId);
                    throw ex;
                }

                try
                {
                    batchId = await ethernaGatewayClient.SystemClient.PostageBatchRefAsync(batchReferenceId);
                }
                catch (EthernaGatewayApiException)
                {
                    //waiting for batchId available
                    await Task.Delay(BatchCheckTimeSpan);
                }
            } while (string.IsNullOrWhiteSpace(batchId));

            Console.WriteLine(". Done");

            await WaitForBatchUsableAsync(batchId);

            return batchId;
        }

        public override Task DeletePinAsync(string hash) =>
            ethernaGatewayClient.ResourcesClient.PinDeleteAsync(hash);

        public override async Task DilutePostageBatchAsync(string batchId, int batchDepth) =>
            await ethernaUserClients.GatewayClient.UsersClient.DiluteAsync(batchId, batchDepth);

        public override async Task<Models.SwarmDtos.PostageBatchDto> GetBatchStatsAsync(string batchId)
        {
            var batchStats = await ethernaUserClients.GatewayClient.UsersClient.BatchesGetAsync(batchId);
            return new Models.SwarmDtos.PostageBatchDto
            {
                BatchTTL = batchStats.BatchTTL,
                BlockNumber = batchStats.BlockNumber,
                BucketDepth = batchStats.BucketDepth,
                Depth = batchStats.Depth,
                Exists = batchStats.Exists,
                Id = batchStats.Id,
                ImmutableFlag = batchStats.ImmutableFlag,
                Label = batchStats.Label,
                Usable = batchStats.Usable,
                Utilization = batchStats.Utilization
            };
        }

        public override async Task<long> GetCurrentChainPriceAsync() =>
            (await ethernaGatewayClient.SystemClient.ChainstateAsync()).CurrentPrice;

        public override async Task<IEnumerable<string>> GetPinnedResourcesAsync() =>
            await ethernaGatewayClient.UsersClient.PinnedResourcesAsync();

        public override async Task<bool> IsBatchUsableAsync(string batchId) =>
            (await ethernaGatewayClient.UsersClient.BatchesGetAsync(batchId)).Usable;

        public override Task OfferContentAsync(string hash) =>
            ethernaGatewayClient.ResourcesClient.OffersPostAsync(hash);
    }
}
