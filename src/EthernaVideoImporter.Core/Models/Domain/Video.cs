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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class Video
    {
        // Constructor.
        public Video(
            VideoMetadataBase metadata,
            SubtitleFile[] subtitleFiles,
            ThumbnailFile[] thumbnailFiles,
            VideoEncodingBase videoEncoding)
        {
            ArgumentNullException.ThrowIfNull(thumbnailFiles, nameof(thumbnailFiles));
            ArgumentNullException.ThrowIfNull(videoEncoding, nameof(videoEncoding));
            
            if (thumbnailFiles.Length == 0)
                throw new ArgumentException("Must exist at least a thumbnail");

            Metadata = metadata;
            SubtitleFiles = subtitleFiles;
            ThumbnailFiles = thumbnailFiles;
            VideoEncoding = videoEncoding;
        }

        // Properties.
        public float AspectRatio
        {
            get
            {
                var largerVideo = VideoEncoding.Variants.MaxBy(v => v.Width) ?? throw new InvalidOperationException();
                return (float)largerVideo.Width / largerVideo.Height;
            }
        }
        public string? EthernaIndexId { get; set; }
        public SwarmHash? EthernaPermalinkHash { get; set; }
        public VideoMetadataBase Metadata { get; }
        public IEnumerable<SubtitleFile> SubtitleFiles { get; }
        public string ThumbnailBlurhash => ThumbnailFiles.First().Blurhash;
        public IEnumerable<ThumbnailFile> ThumbnailFiles { get; }
        public VideoEncodingBase VideoEncoding { get; }
    }
}
