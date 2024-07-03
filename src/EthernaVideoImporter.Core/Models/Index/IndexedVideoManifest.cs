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
using Etherna.Sdk.Users.Index.Models;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Models.Index
{
    public sealed class IndexedVideoManifest
    {
        // Constructors.
        public IndexedVideoManifest(VideoManifest lastValidManifest)
        {
            ArgumentNullException.ThrowIfNull(lastValidManifest, nameof(lastValidManifest));

            BatchId = lastValidManifest.BatchId ?? "";
            Description = lastValidManifest.Description ?? "";
            Duration = lastValidManifest.Duration ?? 0;
            Hash = lastValidManifest.Hash;
            PersonalData = lastValidManifest.PersonalData is null ? null : JsonSerializer.Deserialize<ManifestPersonalDataDto>(lastValidManifest.PersonalData);
            RawPersonalData = lastValidManifest.PersonalData;
            Sources = lastValidManifest.Sources;
            Thumbnail = lastValidManifest.Thumbnail;
            Title = lastValidManifest.Title ?? "";
        }

        // Properties.
        public PostageBatchId BatchId { get; set; }
        public string Description { get; set; }
        public long Duration { get; set; }
        public SwarmHash Hash { get; set; }
        public ManifestPersonalDataDto? PersonalData { get; set; }
        public string? RawPersonalData { get; set; }
        public IEnumerable<VideoSource> Sources { get; set; }
        public Image Thumbnail { get; set; }
        public string Title { get; set; }
    }
}
