using Etherna.ServicesClient.Clients.Gateway;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class EthernaGatewayService : IGatewayService
    {
        // Const.
        private readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);

        // Fields.
        private readonly IUserGatewayClient ethernaGatewayClient;

        // Constructor.
        public EthernaGatewayService(IUserGatewayClient ethernaGatewayClient) 
        {
            this.ethernaGatewayClient = ethernaGatewayClient;
        }

        public async Task<bool> BatchIsUsableAsync(string batchId)
        {
            return (await ethernaGatewayClient.UsersClient.BatchesGetAsync(batchId)).Usable;
        }

        // Methods.
        public async Task<long> ChainCurrentPriceAsync()
        {
            return (await ethernaGatewayClient.SystemClient.ChainstateAsync()).CurrentPrice;
        }

        public async Task<string> CreatePostageBatchAsync(int batchDeep, long amount)
        {
            var batchReferenceId = await ethernaGatewayClient.UsersClient.BatchesPostAsync(batchDeep, amount);

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
                catch (GatewayApiException)
                {
                    //waiting for batchId avaiable
                    await Task.Delay(BatchCheckTimeSpan);
                }
            } while (string.IsNullOrWhiteSpace(batchId));

            Console.WriteLine(". Done");

            return batchId;
        }

        public async Task OffersPostAsync(string hash)
        {
            await ethernaGatewayClient.ResourcesClient.OffersPostAsync(hash);
        }

        public async Task PinDeleteAsync(string hash)
        {
            await ethernaGatewayClient.ResourcesClient.PinDeleteAsync(hash);
        }

        public async Task<IEnumerable<string>> PinnedResourcesAsync()
        {
            return await ethernaGatewayClient.UsersClient.PinnedResourcesAsync();
        }
    }
}
