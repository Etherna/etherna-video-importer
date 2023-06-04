//   Copyright 2022-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using IdentityModel.Client;
using IdentityModel.OidcClient;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.SSO
{
    public static class SignInService
    {
        public static async Task<LoginResult> CodeFlowSigIn()
        {
            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new SystemBrowser(11420);
            var redirectUri = $"http://127.0.0.1:{browser.Port}";

            var options = new OidcClientOptions
            {
                Authority = CommonConsts.EthernaSsoUrl,
                ClientId = CommonConsts.EthernaSsoClientId,
                RedirectUri = redirectUri,
                Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",
                FilterClaims = false,

                Browser = browser,
                RefreshTokenInnerHttpHandler = new SocketsHttpHandler()
            };

            var oidcClient = new OidcClient(options);
            return await oidcClient.LoginAsync(new LoginRequest());
        }

        public static async Task<UserInfoResponse> PasswordFlowGetUserInfoAsync(string accessToken)
        {
            using var client = new HttpClient();
            using var request = new UserInfoRequest
            {
                Address = CommonConsts.EthernaSsoUrl + "connect/userinfo",
                Token = accessToken
            };

            return await client.GetUserInfoAsync(request);
        }

        public static async Task<TokenResponse> PasswordFlowSignInAsync(string apiKey)
        {
            if (apiKey is null)
                throw new ArgumentNullException(nameof(apiKey));

            var splitApiKey = apiKey.Split('.');
            if (splitApiKey.Length != 2)
                throw new ArgumentException("Invalid api key", nameof(apiKey));

            using var client = new HttpClient();
            using var request = new PasswordTokenRequest
            {
                Address = CommonConsts.EthernaSsoUrl + "connect/token",

                ClientId = "apiKeyClientId",
                Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",

                UserName = splitApiKey[0],
                Password = splitApiKey[1]
            };

            return await client.RequestPasswordTokenAsync(request);
        }
    }
}
