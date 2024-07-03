// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

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
