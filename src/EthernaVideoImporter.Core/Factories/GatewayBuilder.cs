using Etherna.BeeNet;
using Etherna.ServicesClient;
using Etherna.VideoImporter.Core.Services;
using System;

namespace Etherna.VideoImporter.Core.Factories
{
    public static class GatewayBuilder
    {
        public static IGatewayService Build(
            bool nativeBeeGateway,
            BeeNodeClient beeNodeClient,
            EthernaUserClients ethernaUserClients)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (ethernaUserClients is null)
                throw new ArgumentNullException(nameof(ethernaUserClients));

            if (nativeBeeGateway)
            {
                /*using var beeNodeClient = new BeeNodeClient(
                    gatewayUrl,
                    CommonConsts.BeeNodeGatewayPort,
                    null,
                    CommonConsts.BeeNodeGatewayVersion,
                    CommonConsts.BeeNodeDebugVersion,
                    httpClient);*/
                return new BeeGatewayService(beeNodeClient.GatewayClient!);
            }
            else
            {
                /*var ethernaUserClients = new EthernaUserClients(
                    new Uri(gatewayUrl),
                    new Uri(gatewayUrl),
                    new Uri(gatewayUrl),
                    new Uri(gatewayUrl),
                    () => httpClient);*/
                return new EthernaGatewayService(ethernaUserClients.GatewayClient);
            }
        }
    }
}
