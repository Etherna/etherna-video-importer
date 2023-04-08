using Etherna.BeeNet.Clients.GatewayApi;
using System;
using System.Net.Http;

namespace Etherna.VideoImporter.Core.Services
{
    public class BeeGatewayClientWrapper : BeeGatewayClient
    {
        // Constructor.
        public BeeGatewayClientWrapper(IHttpClientFactory httpClientFactory)
            : base(
                  PassThroughNonNull(httpClientFactory).CreateClient("ethernaClient"), 
                  new Uri(CommonConsts.EthernaGatewayUrl), 
                  CommonConsts.BeeNodeGatewayVersion)
        { }

        // Helpers.
        private static IHttpClientFactory PassThroughNonNull(IHttpClientFactory httpClientFactory)
        {
            if (httpClientFactory is null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            return httpClientFactory;
        }
    }
}
