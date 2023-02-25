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

using Etherna.VideoImporter.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Dtos
{
    public sealed class ManifestDto
    {
        // Constructors.
        public ManifestDto(
            Video video,
            string batchId,
            string ownerAddress)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));

            Title = video.Metadata.Title;
            Description = video.Metadata.Description;
            OriginalQuality = video.Metadata.OriginVideoQualityLabel;
            OwnerAddress = ownerAddress;
            Duration = (long)video.Metadata.Duration.TotalSeconds;
            Thumbnail = video.ThumbnailFile is null ? null : new ManifestThumbnailDto(video.ThumbnailFile);
            Sources = video.EncodedFiles.OfType<VideoFile>().Select(vf => new ManifestVideoSourceDto(vf));
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            UpdatedAt = null;
            BatchId = batchId;
            PersonalData = JsonSerializer.Serialize(ManifestPersonalDataDto.BuildNew(video.Metadata.Id));
        }

        public ManifestDto(
            string title,
            string description,
            string originalQuality,
            string ownerAddress,
            long duration,
            ManifestThumbnailDto? thumbnail,
            IEnumerable<ManifestVideoSourceDto> sources,
            long createdAt,
            long? updatedAt,
            string batchId,
            string? personalData)
        {
            Title = title;
            Description = description;
            OriginalQuality = originalQuality;
            OwnerAddress = ownerAddress;
            Duration = duration;
            Thumbnail = thumbnail;
            Sources = sources;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            BatchId = batchId;
            PersonalData = personalData;
        }

        // Properties.
        public string Title { get; }
        public string Description { get; }
        public string OriginalQuality { get; }
        public string OwnerAddress { get; }
        public long Duration { get; }
        public ManifestThumbnailDto? Thumbnail { get; }
        public IEnumerable<ManifestVideoSourceDto> Sources { get; }
        public long CreatedAt { get; }
        public long? UpdatedAt { get; }
        public string BatchId { get; }
        public string? PersonalData { get; }
        public string V => "1.2";
    }
}
