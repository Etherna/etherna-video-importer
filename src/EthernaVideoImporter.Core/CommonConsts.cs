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

using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using System;
using System.Runtime.InteropServices;

namespace Etherna.VideoImporter.Core
{
    public sealed class CommonConsts
    {
        public const int BEENODE_GATEWAYPORT = 443;
        public const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v4_0_0;
        public const DebugApiVersion BEENODE_DEBUGVERSION = DebugApiVersion.v4_0_0;
        public const int DOWNLOAD_MAX_RETRY = 3;
        public static readonly TimeSpan DOWNLOAD_RETRY_TIMESPAN = TimeSpan.FromMilliseconds(3500);
        public const string ETHERNA_CREDIT = "https://credit.etherna.io/";
        public const string ETHERNA_INDEX = "https://index.etherna.io/";
        public const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";
        public static string FFMpegBinaryName
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "ffmpeg.exe";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "ffmpeg";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "ffmpeg";

                throw new InvalidOperationException("OS not supported");
            }
        }
        public const string SSO_AUTHORITY = "https://sso.etherna.io/";
        public const string SSO_CLIENT_ID = "ethernaVideoImporterId";
        public const string PREFIX_ETHERNA_INDEX = "https://etherna.io/embed/";
        public const string PREFIX_ETHERNA_PERMALINK = "https://etherna.io/embed/";
    }
}
