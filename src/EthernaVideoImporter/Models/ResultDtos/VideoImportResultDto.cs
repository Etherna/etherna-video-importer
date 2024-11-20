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
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Models.Domain;
using System;

namespace Etherna.VideoImporter.Models.ResultDtos
{
    internal sealed class VideoImportResultDto
    {
        public VideoImportResultDto(VideoImportResultBase result)
        {
            ArgumentNullException.ThrowIfNull(result, nameof(result));
            
            CompletedDateTimeUtc = result.CompletedDateTime;
            SourceMetadata = result.SourceMetadata switch
            {
                JsonVideoMetadata jsonSource => new JsonSourceMetadataDto(jsonSource),
                YouTubeVideoMetadataBase ytSource => new YouTubeSourceMetadataDto(ytSource),
                _ => throw new InvalidOperationException()
            };
            switch (result)
            {
                case VideoImportResultFailed:
                    Succeeded = false;
                    break;
                case VideoImportResultSucceeded succeededResult:
                    IndexId = succeededResult.IndexId;
                    Succeeded = true;
                    SwarmHash = succeededResult.ReferenceHash.ToString();
                    break;
                default: throw new InvalidOperationException();
            }
        }
        
        public DateTime CompletedDateTimeUtc { get; }
        public string? EmbeddedIndexLink => IndexId is null ? null : UrlBuilder.BuildEmbeddedIndexUrl(IndexId);
        public string? EmbeddedPermalink => SwarmHash is null ? null : UrlBuilder.BuildEmbeddedPermalinkUrl(SwarmHash);
        public string? IndexId { get; }
        public string? NormalIndexLink => IndexId is null ? null : UrlBuilder.BuildNormalIndexUrl(IndexId);
        public string? NormalPermalink => SwarmHash is null ? null : UrlBuilder.BuildNormalPermalinkUrl(SwarmHash);
        public SourceMetadataDtoBase SourceMetadata { get; }
        public bool Succeeded { get; }
        public string? SwarmHash { get; }
    }
}