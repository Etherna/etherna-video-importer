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

using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestThumbnailDto
    {
        // Constructors.
        [SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "Is acceptable here")]
        public ManifestThumbnailDto(IEnumerable<IThumbnailFile> thumbnailFiles)
        {
            ArgumentNullException.ThrowIfNull(thumbnailFiles, nameof(thumbnailFiles));
            if (!thumbnailFiles.Any())
                throw new ArgumentOutOfRangeException(nameof(thumbnailFiles), "Thumbnail list can't be empty");

            var maxQualityThumb = thumbnailFiles.OrderByDescending(t => t.Width).First();
            AspectRatio = maxQualityThumb.AspectRatio;
            Blurhash = maxQualityThumb.Blurhash;
            Sources = thumbnailFiles.ToDictionary(
                t => $"{t.Width}w",
                t => t.SwarmHash.ToString()!);
        }

        // Properties.
        public float AspectRatio { get; }
        public string Blurhash { get; }
        public IDictionary<string, string> Sources { get; }
    }
}
