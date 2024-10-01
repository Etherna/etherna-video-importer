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

using System.Text.Json.Serialization;

namespace Etherna.VideoImporter.Devcon.Models.JsonDto
{
    internal sealed class DevconFileDto
    {
        // Consts.
        public const string IpfsHashKey = "sources_ipfsHash";
        public const string SwarmHashKey = "sources_swarmHash";
        public const string YoutubeIdKey = "sources_youtubeId";
        
        // Properties.
        [JsonPropertyName("description")]
        public string Description { get; set; } = default!;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = default!;
        
        [JsonPropertyName(SwarmHashKey)]
        public string? SwarmHash { get; set; }
        
        [JsonPropertyName(YoutubeIdKey)]
        public string YoutubeId { get; set; } = default!;
    }
}
