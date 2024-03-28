//   Copyright 2022-present Etherna SA
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

using Etherna.Sdk.GeneratedClients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Models.Index
{
    public sealed class IndexedVideoManifest
    {
        // Constructors.
        public IndexedVideoManifest(VideoManifestDto lastValidManifest)
        {
            ArgumentNullException.ThrowIfNull(lastValidManifest, nameof(lastValidManifest));

            BatchId = lastValidManifest.BatchId ?? "";
            Description = lastValidManifest.Description ?? "";
            Duration = lastValidManifest.Duration ?? 0;
            Hash = lastValidManifest.Hash;
            OriginalQuality = lastValidManifest.OriginalQuality ?? "";
            PersonalData = lastValidManifest.PersonalData is null ? null : JsonSerializer.Deserialize<ManifestPersonalDataDto>(lastValidManifest.PersonalData);
            RawPersonalData = lastValidManifest.PersonalData;
            Sources = lastValidManifest.Sources;
            Thumbnail = lastValidManifest.Thumbnail;
            Title = lastValidManifest.Title ?? "";
        }

        // Properties.
        public string BatchId { get; set; }
        public string Description { get; set; }
        public long Duration { get; set; }
        public string Hash { get; set; }
        public bool HasLastSpecifications
        {
            get
            {
                // If client version is missing (0.1.x or 0.2.x).
                if (string.IsNullOrWhiteSpace(PersonalData?.ClientVersion))
                    return false;

                // If has invalid version.
                if (!Version.TryParse(PersonalData.ClientVersion, out var clientVersion))
                    return false;

                // Select by client version.
                return clientVersion switch
                {
                    { Major: 0, Minor: <= 2 } => false,
                    _ => true
                };
            }
        }
        public string OriginalQuality { get; set; }
        public ManifestPersonalDataDto? PersonalData { get; set; }
        public string? RawPersonalData { get; set; }
        public IEnumerable<SourceDto> Sources { get; set; }
        public ImageDto Thumbnail { get; set; }
        public string Title { get; set; }

        // Methods.
        public bool HasEqualMetadata(VideoMetadataBase sourceMetadata)
        {
            ArgumentNullException.ThrowIfNull(sourceMetadata, nameof(sourceMetadata));

            return PersonalData?.VideoIdHash == sourceMetadata.SourceIdHash &&
                   Title == sourceMetadata.Title &&
                   Description == sourceMetadata.Description;
        }
    }
}
