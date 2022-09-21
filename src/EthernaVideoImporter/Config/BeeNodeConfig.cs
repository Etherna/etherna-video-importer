using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;

namespace Etherna.EthernaVideoImporter.Config
{
    internal class BeeNodeConfig
    {
        public string? Url { get; set; }
        public int GatewayPort { get; set; }
        public GatewayApiVersion GatewayVersion { get; set; }
        public int DebugPort { get; set; }
        public DebugApiVersion DebugVersion { get; set; }
    }
}
