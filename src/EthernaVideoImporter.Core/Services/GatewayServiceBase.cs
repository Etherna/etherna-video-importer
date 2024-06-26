﻿// Copyright 2022-present Etherna SA
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

using Etherna.BeeNet;
using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.IO;
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
        protected readonly IBeeClient beeClient;
        private readonly IIoService ioService;
#pragma warning restore CA1051 // Do not declare visible instance fields

        // Constructor.
        protected GatewayServiceBase(
            IBeeClient beeClient,
            IIoService ioService)
        {
            this.beeClient = beeClient;
            this.ioService = ioService;
        }

        // Methods.
        public abstract Task<PostageBatchId> CreatePostageBatchAsync(BzzBalance amount, int batchDepth);

        public abstract Task DefundResourcePinningAsync(SwarmHash hash);

        public abstract Task FundResourceDownloadAsync(SwarmHash hash);

        public abstract Task<BzzBalance> GetChainPriceAsync();

        public abstract Task<IEnumerable<SwarmHash>> GetPinnedResourcesAsync();

        public abstract Task<bool> IsBatchUsableAsync(PostageBatchId batchId);

        public Task<SwarmHash> UploadFileAsync(
            PostageBatchId batchId,
            Stream content,
            string? name,
            string? contentType,
            bool swarmPin) =>
            beeClient.UploadFileAsync(
                batchId,
                content,
                name: name,
                contentType: contentType,
                swarmPin: swarmPin);

        // Protected methods.
        protected async Task WaitForBatchUsableAsync(PostageBatchId batchId)
        {
            // Wait until created batch is usable.
            ioService.PrintTimeStamp();
            ioService.Write("Waiting for batch being usable... (it may take a while)");

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

            ioService.WriteLine(". Done", false);
        }
    }
}
