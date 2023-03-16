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

using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
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

            if (!video.ThumbnailFiles.Any())
                Thumbnail = null;
            else
            {
                // Check thumbnail homogeneous type
                var swarmThumbnail = video.ThumbnailFiles.All(i => i is SwarmThumbnail);
                var thumbnailFile = video.ThumbnailFiles.All(i => i is ThumbnailFile);

                if (!swarmThumbnail &&
                    !thumbnailFile)
                {
                    throw new InvalidOperationException("Mixed source");
                }
                if (swarmThumbnail)
                {
                    var thumbnailFiles = video.ThumbnailFiles.Cast<SwarmThumbnail>().ToList();
                    Thumbnail = new ManifestThumbnailDto(thumbnailFiles.First().AspectRatio, thumbnailFiles.First().Blurhash, thumbnailFiles.ToDictionary(t => t.Resolution!, t => t.UploadedHashReference!));
                }
                else
                    Thumbnail = new ManifestThumbnailDto(video.ThumbnailFiles.Cast<ThumbnailFile>());
            }

            Title = video.Metadata.Title;
            Description = video.Metadata.Description;
            OriginalQuality = video.Metadata.OriginVideoQualityLabel;
            OwnerAddress = ownerAddress;
            Duration = (long)video.Metadata.Duration.TotalSeconds;
            Sources = video.EncodedFiles.OfType<VideoFile>().Select(vf => new ManifestVideoSourceDto(vf));
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            UpdatedAt = null;
            BatchId = batchId;
            PersonalData = JsonSerializer.Serialize(ManifestPersonalDataDto.BuildNew(video.Metadata.Id));
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
