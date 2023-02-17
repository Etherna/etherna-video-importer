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

using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Dtos
{
    public sealed class ManifestDto
    {
        // Constructors.
        public ManifestDto(
            long createdAt,
            string ownerAddress,
            string? batchId,
            string? description,
            long? duration,
            string? originalQuality,
            string? personalData,
            ManifestThumbnailDto? thumbnail,
            string? title,
            IEnumerable<ManifestVideoSourceDto> sources)
        {
            CreatedAt = createdAt;
            OwnerAddress = ownerAddress;
            BatchId = batchId;
            Description = description;
            Duration = duration;
            Hash = "";
            OriginalQuality = originalQuality;
            PersonalData = personalData;
            Thumbnail = thumbnail;
            Title = title;
            Sources = sources;
        }

        // Properties.
        public long CreatedAt { get; }
        public string? BatchId { get; }
        public string? Description { get; }
        public long? Duration { get; }
        public string Hash { get; }
        public string? OriginalQuality { get; }
        public string OwnerAddress { get; }
        public string? PersonalData { get; }
        public long? UpdatedAt { get; }
        public IEnumerable<ManifestVideoSourceDto> Sources { get; }
        public ManifestThumbnailDto? Thumbnail { get; }
        public string? Title { get; }
        public string V { get; } = "1.1";
    }
}
