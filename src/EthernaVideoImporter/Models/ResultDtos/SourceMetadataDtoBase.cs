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
using System.Text.Json.Serialization;

namespace Etherna.VideoImporter.Models.ResultDtos
{
    [JsonDerivedType(typeof(JsonSourceMetadataDto))]
    [JsonDerivedType(typeof(YouTubeSourceMetadataDto))]
    public abstract class SourceMetadataDtoBase
    {
        protected internal SourceMetadataDtoBase(VideoMetadataBase sourceMetadata)
        {
            ArgumentNullException.ThrowIfNull(sourceMetadata, nameof(sourceMetadata));
            
            SourceId = sourceMetadata.SourceId;
            Duration = sourceMetadata.Duration;
            Title = sourceMetadata.Title;
        }
        
        // Properties.
        public string SourceId { get; }
        public TimeSpan Duration { get; }
        public string Title { get; }
    }
}