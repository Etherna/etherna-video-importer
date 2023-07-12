﻿//   Copyright 2022-present Etherna Sagl
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
        // Consts.
        private const int PersonalDataMaxLength = 200;

        // Fields.
        private string? _personalData;

        // Constructors.
        public ManifestDto(
            Video video,
            string batchId,
            string ownerAddress,
            bool allowFakeReferences = false)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));

            Title = video.Metadata.Title;
            Description = video.Metadata.Description;
            OriginalQuality = video.Metadata.OriginVideoQualityLabel;
            OwnerAddress = ownerAddress;
            Duration = (long)video.Metadata.Duration.TotalSeconds;
            Sources = video.EncodedFiles.OfType<IVideoFile>().Select(vf => new ManifestVideoSourceDto(vf, allowFakeReferences));
            Thumbnail = video.ThumbnailFiles.Any() ?
                new ManifestThumbnailDto(video.ThumbnailFiles) :
                null;
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
