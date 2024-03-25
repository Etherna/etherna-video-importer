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

using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestDto
    {
        // Consts.
        private const int PersonalDataMaxLength = 200;

        // Fields.
        private string? _personalData;

        // Constructors.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private ManifestDto() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Builders.
        public static async Task<ManifestDto> BuildNewAsync(
            Video video,
            string batchId,
            string ownerAddress,
            bool allowFakeReferences = false)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));

            var sources = new List<ManifestVideoSourceDto>();
            foreach (var videoFile in video.EncodedFiles.OfType<IVideoFile>())
                sources.Add(await ManifestVideoSourceDto.BuildNewAsync(videoFile, allowFakeReferences));

            return new ManifestDto()
            {
                Title = video.Metadata.Title,
                Description = video.Metadata.Description,
                OriginalQuality = video.Metadata.OriginVideoQualityLabel,
                OwnerAddress = ownerAddress,
                Duration = (long)video.Metadata.Duration.TotalSeconds,
                Sources = sources,
                Thumbnail = video.ThumbnailFiles.Any() ?
                    new ManifestThumbnailDto(video.ThumbnailFiles) :
                    null,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UpdatedAt = null,
                BatchId = batchId,
                PersonalData = JsonSerializer.Serialize(ManifestPersonalDataDto.BuildNew(video.Metadata.Id))
            };
        }

        // Properties.
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string OriginalQuality { get; private set; }
        public string OwnerAddress { get; private set; }
        public long Duration { get; private set; }
        public ManifestThumbnailDto? Thumbnail { get; private set; }
        public IEnumerable<ManifestVideoSourceDto> Sources { get; private set; }
        public long CreatedAt { get; private set; }
        public long? UpdatedAt { get; private set; }
        public string BatchId { get; private set; }
        public string? PersonalData
        {
            get => _personalData;
            set
            {
                if (value is not null && value.Length > PersonalDataMaxLength)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _personalData = value;
            }
        }
        public string V => "1.2";
    }
}
