﻿//   Copyright 2022-present Etherna SA
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

using Etherna.ServicesClient.GeneratedClients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;

namespace Etherna.VideoImporter.Core.Models.Index
{
    public sealed class IndexedVideo
    {
        // Constructors.
        public IndexedVideo(VideoDto video)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));

            IndexId = video.Id;
            CreationDateTime = video.CreationDateTime;
            if (video.LastValidManifest is not null)
                LastValidManifest = new IndexedVideoManifest(video.LastValidManifest);
        }

        // Properties.
        public string IndexId { get; set; }
        public DateTimeOffset CreationDateTime { get; set; }
        public IndexedVideoManifest? LastValidManifest { get; set; }

        // Internal methods.
        internal bool IsEqualTo(VideoMetadataBase sourceMetadata) =>
            LastValidManifest?.PersonalData?.VideoIdHash == ManifestPersonalDataDto.HashVideoId(sourceMetadata.Id) &&
            LastValidManifest?.Title == sourceMetadata.Title &&
            LastValidManifest?.Description == sourceMetadata.Description;
    }
}
