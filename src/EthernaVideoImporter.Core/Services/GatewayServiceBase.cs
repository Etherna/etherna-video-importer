using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.BeeNet.InputModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public abstract class GatewayServiceBase : IGatewayService
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        // Consts.
        protected readonly TimeSpan BatchCheckTimeSpan = new(0, 0, 0, 5);
        protected readonly TimeSpan BatchUsableTimeout = new(0, 0, 10, 0);

        // Fields.
        protected readonly IBeeGatewayClient beeGatewayClient;
#pragma warning restore CA1051 // Do not declare visible instance fields

        // Constructor.
        protected GatewayServiceBase(IBeeGatewayClient beeGatewayClient)
        {
            this.beeGatewayClient = beeGatewayClient;
        }

        // Methods.
        public abstract Task<string> CreatePostageBatchAsync(long amount, int batchDepth);

        public abstract Task DeletePinAsync(string hash);

        public abstract Task<long> GetCurrentChainPriceAsync();

        public abstract Task<IEnumerable<string>> GetPinnedResourcesAsync();

        public abstract Task<bool> IsBatchUsableAsync(string batchId);

        public abstract Task OfferContentAsync(string hash);

        public Task<string> UploadFilesAsync(
            string batchId,
            IEnumerable<FileParameterInput> files,
            bool swarmPin) =>
            beeGatewayClient.UploadFileAsync(
                batchId,
                files,
                swarmDeferredUpload: true,
                swarmPin: swarmPin);

        // Protected methods.
        protected async Task WaitForBatchUsableAsync(string batchId)
        {
            // Wait until created batch is usable.
            Console.Write("Waiting for batch being usable... (it may take a while)");

            var batchStartWait = DateTime.UtcNow;
            bool batchIsUsable;
            do
            {
                //timeout throw exception
                if (DateTime.UtcNow - batchStartWait >= BatchUsableTimeout)
                {
                    var ex = new InvalidOperationException("Batch not usable after timeout");
                    ex.Data.Add("BatchId", batchId);
                    throw ex;
                }

                batchIsUsable = await IsBatchUsableAsync(batchId);

                //waiting for batch usable
                await Task.Delay(BatchCheckTimeSpan);
            } while (!batchIsUsable);

            Console.WriteLine(". Done");
        }
    }
}
