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
using System.IO;
using System.Runtime.InteropServices;

namespace Etherna.VideoImporter.Core
{
    public sealed class CommonConsts
    {
        public const int BeeNodeGatewayPort = 443;
        public const GatewayApiVersion BeeNodeGatewayVersion = GatewayApiVersion.v4_0_0;
        public const DebugApiVersion BeeNodeDebugVersion = DebugApiVersion.v4_0_0;
        public const int DownloadMaxRetry = 3;
        public static readonly TimeSpan DownloadTimespanRetry = TimeSpan.FromMilliseconds(3500);
        public const string EthernaCreditUrl = "https://credit.etherna.io/";
        public const string EthernaIndexUrl = "https://index.etherna.io/";
        public const string EthernaIndexContentUrlPrefix = "https://etherna.io/embed/";
        public const string EthernaGatewayUrl = "https://gateway.etherna.io/";
        public const string EthernaPermalinkContentUrlPrefix = "https://etherna.io/embed/";
        public const string EthernaSsoClientId = "ethernaVideoImporterId";
        public const string EthernaSsoUrl = "https://sso.etherna.io/";
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
        public static readonly TimeSpan GnosisBlockTime = TimeSpan.FromSeconds(5);
        public const string ImporterIdentifier = "EthernaImporter";
    }
}
