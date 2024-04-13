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
using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Devcon.Models.Domain
{
    internal sealed class MdFileVideoMetadata : YouTubeVideoMetadataBase
    {
        // Fields.
        private readonly string descriptionOverride;
        private readonly string titleOverride;
        
        // Constructor.
        public MdFileVideoMetadata(
            string title,
            string description,
            string mdFileRelativePath,
            IYoutubeDownloader youtubeDownloader,
            string youtubeUrl,
            string? ethernaIndexUrl,
            string? ethernaPermalinkUrl)
            : base(youtubeDownloader, youtubeUrl, null)
        {
            if (string.IsNullOrWhiteSpace(mdFileRelativePath))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(mdFileRelativePath));
            
            // Generate Id and old Ids.
            Id = mdFileRelativePath.Replace('\\', '/'); //use unix-like path
            OldIds = new[] { mdFileRelativePath.Replace('/', '\\') }; //migrate from windows-like path

            descriptionOverride = description;
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFileRelativePath = mdFileRelativePath;
            titleOverride = title;
        }

        // Properties.
        public override string Id { get; }
        public override string Description
        {
            get => descriptionOverride;
            protected set { }
        }
        public string? EthernaIndexUrl { get; }
        public string? EthernaPermalinkUrl { get; }
        public string MdFileRelativePath { get; }
        public override IEnumerable<string> OldIds { get; }
        public override string Title
        {
            get => titleOverride;
            protected set { }
        }
    }
}
