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

using Etherna.BeeNet.Models;
using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestVideoSourceDto
    {
        // Constructors.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private ManifestVideoSourceDto() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Builders.
        public static async Task<ManifestVideoSourceDto> BuildNewAsync(
            IVideoFile videoFile,
            bool allowFakeReference)
        {
            ArgumentNullException.ThrowIfNull(videoFile, nameof(videoFile));

            return new ManifestVideoSourceDto
            {
                Quality = videoFile.VideoQualityLabel,
                Reference = (videoFile.SwarmHash ??
                             (allowFakeReference ? SwarmHash.Zero : throw new InvalidOperationException())).ToString(),
                Size = await videoFile.GetByteSizeAsync()
            };
        }

        // Properties.
        public int Bitrate => 420; //fake place holder, will be removed on manifest 2.0
        public string Quality { get; private set; }
        public string Reference { get; private set; }
        public long Size { get; private set; }
    }
}
