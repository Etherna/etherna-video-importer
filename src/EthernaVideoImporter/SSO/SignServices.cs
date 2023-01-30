using Etherna.EthernaVideoImporter.Utilities;
using IdentityModel.OidcClient;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.SSO
{
    static internal class SignServices
    {
        public static async Task<LoginResult> SigInSSO()
        {
            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new SystemBrowser(11420);
            var redirectUri = $"http://127.0.0.1:{browser.Port}";

            var options = new OidcClientOptions
            {
                Authority = CommonConst.SSO_AUTHORITY,
                ClientId = CommonConst.SSO_CLIENT_ID,
                RedirectUri = redirectUri,
                Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",
                FilterClaims = false,

                Browser = browser,
                RefreshTokenInnerHttpHandler = new SocketsHttpHandler()
            };

            var oidcClient = new OidcClient(options);
            return await oidcClient.LoginAsync(new LoginRequest()).ConfigureAwait(false);
        }
    }
}
