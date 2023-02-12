﻿//   Copyright 2022-present Etherna Sagl
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

using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;

namespace Etherna.VideoImporter.Core.Utilities
{
    public sealed class CommonConst
    {
        public const int BEENODE_GATEWAYPORT = 443;
        public const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
        public const DebugApiVersion BEENODE_DEBUGVERSION = DebugApiVersion.v3_0_2;
        public const string ETHERNA_CREDIT = "https://credit.etherna.io/";
        public const string ETHERNA_INDEX = "https://index.etherna.io/";
        public const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";
        public const string SSO_AUTHORITY = "https://sso.etherna.io/";
        public const string SSO_CLIENT_ID = "ethernaVideoImporterId";
        public const string PREFIX_ETHERNA_INDEX = "https://etherna.io/embed/";
        public const string PREFIX_ETHERNA_PERMALINK = "https://etherna.io/embed/";
    }
}
