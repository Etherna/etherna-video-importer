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

using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    internal sealed class SwarmVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SwarmVideoMetadata(
            string id,
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel)
        {
            Id = id;
            Description = description;
            Duration = duration;
            OriginVideoQualityLabel = originVideoQualityLabel;
            Title = title;
        }

        // Properties.
        public override string Id { get; }
        public override IEnumerable<string> OldIds => Array.Empty<string>();
    }
}
