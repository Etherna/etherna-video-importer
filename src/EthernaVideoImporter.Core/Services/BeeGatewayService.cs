using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.BeeNet.DtoModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class BeeGatewayService : IGatewayService
    {
        // Const.
        private readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        private readonly TimeSpan BatchCreationTimeout = new(0, 0, 10, 0);

        // Fields.
        private readonly IBeeGatewayClient beeGatewayClient;

        // Constructor.
        public BeeGatewayService(IBeeGatewayClient beeGatewayClient)
        {
            this.beeGatewayClient = beeGatewayClient;
        }

        // Methods.
        public async Task<bool> BatchIsUsableAsync(string batchId)
        {
            var batch = await beeGatewayClient.GetPostageBatchAsync(batchId);

            return batch.Usable;
        }

        public async Task<long> ChainCurrentPriceAsync()
        {
            var chainState = await beeGatewayClient.GetChainStateAsync();
            
            return chainState.CurrentPrice;
        }

        public async Task<string> CreatePostageBatchAsync(int batchDeep, long amount)
        {
            Console.Write("Waiting for batch created... (it may take a while)");
            var batchId  = await beeGatewayClient.BuyPostageBatchAsync(amount, batchDeep); //Label or other fields to send?

            // Wait until created batch is avaiable.
            PostageBatchDto? postageBatchDto = null;
            var batchStartWait = DateTime.UtcNow;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchCreationTimeout)
                {
                    var ex = new InvalidOperationException("Batch not avaiable after timeout");
                    ex.Data.Add("batchId", batchId);
                    throw ex;
                }

                try
                {
                    postageBatchDto = await beeGatewayClient.GetPostageBatchAsync(batchId);
                }
                catch (Exception)
                {
                    //waiting for batch avaiable
                    await Task.Delay(BatchCheckTimeSpan);
                }
            } while (postageBatchDto is null);

            Console.WriteLine(". Done");

            return postageBatchDto.Id;
        }

        public Task OffersPostAsync(string hash)
        {
            throw new NotImplementedException(); //Not supported in native node?
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
