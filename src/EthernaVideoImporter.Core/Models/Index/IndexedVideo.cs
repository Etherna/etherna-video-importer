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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using Video = Etherna.Sdk.Users.Index.Models.Video;

namespace Etherna.VideoImporter.Core.Models.Index
{
    public sealed class IndexedVideo
    {
        // Constructors.
        public IndexedVideo(Video video)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));

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
