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
using Etherna.VideoImporter.Core.Utilities;
using Etherna.VideoImporter.Models.Domain;
using System;

namespace Etherna.VideoImporter.Models.ResultDtos
{
    public class VideoImportResultDto
    {
        public VideoImportResultDto(VideoImportResultBase result)
        {
            ArgumentNullException.ThrowIfNull(result, nameof(result));
            
            CompletedDateTime = result.CompletedDateTime;
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
                    SwarmHash = succeededResult.ReferenceHash;
                    break;
                default: throw new InvalidOperationException();
            }
        }
        
        public DateTime CompletedDateTime { get; }
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