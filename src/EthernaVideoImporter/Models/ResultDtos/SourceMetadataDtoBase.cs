// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
            
            Id = sourceMetadata.Id;
            Duration = sourceMetadata.Duration;
            Title = sourceMetadata.Title;
        }
        
        // Properties.
        public string Id { get; }
        public TimeSpan Duration { get; }
        public string Title { get; }
    }
}