//   Copyright 2022-present Etherna SA
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

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class SourceVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SourceVideoMetadata(
            string id,
            string title,
            string description,
            IEnumerable<string>? oldIds,
            VideoSourceFile sourceVideo,
            ThumbnailSourceFile? sourceThumbnail)
            : base(title, description, sourceVideo.Duration, sourceVideo.VideoQualityLabel)
        {
            Id = id;
            OldIds = oldIds ?? Array.Empty<string>();
            SourceThumbnail = sourceThumbnail;
            SourceVideo = sourceVideo;
        }

        // Properties.
        public override string Id { get; }
        public override IEnumerable<string> OldIds { get; }
        public ThumbnailSourceFile? SourceThumbnail { get; }
        public VideoSourceFile SourceVideo { get; }
    }
}
