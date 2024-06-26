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

using Etherna.BeeNet.Models;
using Etherna.Sdk.Common.Models;
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
