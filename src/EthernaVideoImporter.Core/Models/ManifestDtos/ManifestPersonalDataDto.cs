﻿// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Epoche;
using System;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public class ManifestPersonalDataDto
    {
        // Properties.
        public string? ClientName { get; set; }
        public string? ClientVersion { get; set; }
        public string? VideoIdHash { get; set; }

        // Public methods.
        internal static ManifestPersonalDataDto BuildNew(
            string videoId,
            Version appCurrentVersion) => new()
        {
            ClientName = CommonConsts.ImporterIdentifier,
            ClientVersion = appCurrentVersion.ToString(),
            VideoIdHash = HashVideoId(videoId),
        };

        public static string HashVideoId(string videoId) =>
            BitConverter.ToString(Keccak256.ComputeHash(videoId)).Replace("-", "", StringComparison.OrdinalIgnoreCase);
    }
}
