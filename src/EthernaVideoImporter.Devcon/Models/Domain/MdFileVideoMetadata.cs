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
            SourceId = mdFileRelativePath.Replace('\\', '/'); //use unix-like path
            SourceOldIds = new[] { mdFileRelativePath.Replace('/', '\\') }; //migrate from windows-like path

            descriptionOverride = description;
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFileRelativePath = mdFileRelativePath;
            titleOverride = title;
        }

        // Properties.
        public override string Description
        {
            get => descriptionOverride;
            protected set { }
        }
        public string? EthernaIndexUrl { get; }
        public string? EthernaPermalinkUrl { get; }
        public string MdFileRelativePath { get; }
        public override string SourceId { get; }
        public override IEnumerable<string> SourceOldIds { get; }
        public override string Title
        {
            get => titleOverride;
            protected set { }
        }
    }
}
