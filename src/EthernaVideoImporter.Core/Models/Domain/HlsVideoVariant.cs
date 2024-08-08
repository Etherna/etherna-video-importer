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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class HlsVideoVariant : VideoVariantBase
    {
        // Constructor.
        public HlsVideoVariant(
            FileBase hlsStreamPlaylistFile,
            FileBase[] hlsSegmentFiles,
            int height,
            int width)
            : base(height, width)
        {
            ArgumentNullException.ThrowIfNull(hlsStreamPlaylistFile, nameof(hlsStreamPlaylistFile));
            ArgumentNullException.ThrowIfNull(hlsSegmentFiles, nameof(hlsSegmentFiles));
            if (hlsSegmentFiles.Length == 0)
                throw new ArgumentException("Segments list can't be empty");
            
            EntryFile = hlsStreamPlaylistFile;
            HlsSegmentFiles = hlsSegmentFiles.AsReadOnly();
            TotalByteSize = hlsStreamPlaylistFile.ByteSize + hlsSegmentFiles.Sum(s => s.ByteSize);
        }

        // Properties.
        public override FileBase EntryFile { get; }
        public IReadOnlyCollection<FileBase> HlsSegmentFiles { get; }
        public override long TotalByteSize { get; }
    }
}