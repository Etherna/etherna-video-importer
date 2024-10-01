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

using Etherna.BeeNet.Models;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Services;
using Etherna.VideoImporter.Core.Utilities;
using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Devcon.Models.Domain
{
    internal sealed class DevconFileVideoMetadata : YouTubeVideoMetadataBase
    {
        // Fields.
        private readonly string descriptionOverride;
        private readonly string titleOverride;
        
        // Constructor.
        public DevconFileVideoMetadata(
            string title,
            string description,
            string devconFileRelativePath,
            IYoutubeDownloader youtubeDownloader,
            string youtubeId,
            SwarmHash? swarmHash,
            IVideoProvider videoProvider)
            : base(youtubeDownloader, "https://youtu.be/" + youtubeId, null, videoProvider)
        {
            if (string.IsNullOrWhiteSpace(devconFileRelativePath))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(devconFileRelativePath));
            
            // Generate Id and old Ids.
            SourceId = devconFileRelativePath.Replace('\\', '/'); //use unix-like path
            SourceOldIds = new[] { devconFileRelativePath.Replace('/', '\\') }; //migrate from windows-like path

            descriptionOverride = description;
            DevconFileRelativePath = devconFileRelativePath;
            SwarmHash = swarmHash;
            titleOverride = title;
        }

        // Properties.
        public override string Description
        {
            get => descriptionOverride;
            protected set { }
        }
        public string DevconFileRelativePath { get; }
        public override string SourceId { get; }
        public override IEnumerable<string> SourceOldIds { get; }
        public SwarmHash? SwarmHash { get; }
        public override string Title
        {
            get => titleOverride;
            protected set { }
        }
    }
}
