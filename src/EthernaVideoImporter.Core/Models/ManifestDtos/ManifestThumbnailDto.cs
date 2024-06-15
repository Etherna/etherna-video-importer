// Copyright 2022-present Etherna SA
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
