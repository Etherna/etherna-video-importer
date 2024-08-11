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
using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class JsonVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal JsonVideoMetadata(
            string sourceId,
            string title,
            string description,
            IEnumerable<string>? oldIds,
            VideoEncodingBase videoEncoding,
            ThumbnailFile sourceThumbnail)
        {
            Description = description;
            Duration = videoEncoding.Duration;
            SourceId = sourceId;
            SourceOldIds = oldIds ?? Array.Empty<string>();
            SourceThumbnail = sourceThumbnail;
            VideoEncoding = videoEncoding;
            Title = title;
        }

        // Properties.
        public override string SourceId { get; }
        public override string SourceName => "json";
        public override IEnumerable<string> SourceOldIds { get; }
        public ThumbnailFile SourceThumbnail { get; }
        public VideoEncodingBase VideoEncoding { get; }
    }
}
