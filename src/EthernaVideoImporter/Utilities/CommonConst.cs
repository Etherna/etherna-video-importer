using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;

namespace Etherna.EthernaVideoImporter.Utilities
{
    public sealed class CommonConst
    {
        public const int BEENODE_GATEWAYPORT = 443;
        public const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
        public const DebugApiVersion BEENODE_DEBUGAVERSION = DebugApiVersion.v3_0_2;
        public const string ETHERNA_CREDIT = "https://credit.etherna.io/";
        public const string ETHERNA_INDEX = "https://index.etherna.io/";
        public const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";
        public const string SSO_AUTHORITY = "https://sso.etherna.io/";
        public const string SSO_CLIENT_ID = "ethernaVideoImporterId";
        public const string PREFIX_ETHERNA_INDEX = "https://etherna.io/embed/";
        public const string PREFIX_ETHERNA_PERMALINK = "https://etherna.io/embed/";
    }
}
