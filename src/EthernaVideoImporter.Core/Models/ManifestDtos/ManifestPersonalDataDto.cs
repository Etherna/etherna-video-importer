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

using System;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public class ManifestPersonalDataDto
    {
        // Properties.
        public string? ClientName { get; set; }
        public string? ClientVersion { get; set; }
        public string? VideoId { get; set; }

        // Public methods.
        public static ManifestPersonalDataDto BuildNew(string videoId) => new()
        {
            ClientName = CommonConsts.ImporterIdentifier,
            ClientVersion = EthernaVersionControl.CurrentVersion.ToString(),
            VideoId = videoId
            VideoId = Keccak256(videoId)
        };

        private static string Keccak256(string input)
        {
            var hashAlgorithm = new Org.BouncyCastle.Crypto.Digests.Sha3Digest(256);
            byte[] byte_array = System.Text.Encoding.UTF8.GetBytes(input);
            hashAlgorithm.BlockUpdate(byte_array, 0, byte_array.Length);
            byte[] result = new byte[32]; // 256 / 8 = 32
            hashAlgorithm.DoFinal(result, 0);
            string hashString = BitConverter.ToString(result);
            hashString = hashString.Replace("-", "", StringComparison.InvariantCulture).ToUpperInvariant();
            return hashString;
        }
    }
}
