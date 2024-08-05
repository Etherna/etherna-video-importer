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

using Etherna.BeeNet.Hashing;
using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Models.Domain;
using Nethereum.Hex.HexConvertors.Extensions;
using System;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class IndexedVideoExtensions
    {
        public static bool HasEqualMetadata(this IndexedVideo indexedVideo, VideoMetadataBase metadata, IHasher hasher)
        {
            ArgumentNullException.ThrowIfNull(hasher, nameof(hasher));
            ArgumentNullException.ThrowIfNull(indexedVideo, nameof(indexedVideo));
            ArgumentNullException.ThrowIfNull(metadata, nameof(metadata));

            if (indexedVideo.LastValidManifest is null)
                return false;

            var indexedManifest = indexedVideo.LastValidManifest.Manifest;
            
            return indexedManifest.PersonalData?.SourceVideoId == hasher.ComputeHash(metadata.SourceId).ToHex() &&
                   indexedManifest.Title == metadata.Title &&
                   indexedManifest.Description == metadata.Description;
        }
    }
}