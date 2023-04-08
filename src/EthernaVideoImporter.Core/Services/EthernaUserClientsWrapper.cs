using Etherna.ServicesClient;
using System;
using System.Net.Http;

namespace Etherna.VideoImporter.Core.Services
{
    public class EthernaUserClientsWrapper : EthernaUserClients
    {
        // Constructor.
        public EthernaUserClientsWrapper(IHttpClientFactory httpClientFactory) 
            : base(new Uri(CommonConsts.EthernaCreditUrl),
                  new Uri(CommonConsts.EthernaGatewayUrl),
                  new Uri(CommonConsts.EthernaIndexUrl),
                  new Uri(CommonConsts.EthernaIndexUrl), 
                  () => httpClientFactory.CreateClient("ethernaClient"))
        {}
    }
}
