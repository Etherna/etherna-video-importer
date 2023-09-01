//   Copyright 2022-present Etherna Sagl
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
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Devcon.Models.Domain
{
    public class MdFileVideoMetadata : YouTubeVideoMetadataBase
    {
        // Constructor.
        public MdFileVideoMetadata(
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            Thumbnail? thumbnail,
            string mdFileRelativePath,
            string youtubeUrl,
            string? ethernaIndexUrl,
            string? ethernaPermalinkUrl)
            : base(title, description, duration, originVideoQualityLabel, thumbnail, youtubeUrl)
        {
            if (string.IsNullOrWhiteSpace(mdFileRelativePath))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(mdFileRelativePath));
            
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFileRelativePath = mdFileRelativePath;
            
            // Generate Id and old Ids.
            Id = mdFileRelativePath.Replace('\\', '/'); //use unix-like path
            OldIds = new[] { mdFileRelativePath.Replace('/', '\\') }; //migrate from windows-like path
        }

        // Properties.
        public override string Id { get; }
        public string? EthernaIndexUrl { get; }
        public string? EthernaPermalinkUrl { get; }
        public string MdFileRelativePath { get; }
        public override IEnumerable<string> OldIds { get; }
    }
}
