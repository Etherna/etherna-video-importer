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

using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class Video
    {
        // Constructor.
        public Video(
            VideoMetadataBase metadata,
            IEnumerable<FileBase> encodedFiles,
            IEnumerable<FileBase> thumbnailFiles)
        {
            if (!encodedFiles.Any())
                throw new ArgumentException("Must exist at least a stream");

            EncodedFiles = encodedFiles;
            Metadata = metadata;
            ThumbnailFiles = thumbnailFiles;
        }

        // Properties.
        public IEnumerable<FileBase> EncodedFiles { get; }
        public string? EthernaIndexId { get; set; }
        public string? EthernaPermalinkHash { get; set; }
        public VideoMetadataBase Metadata { get; }
        public IEnumerable<FileBase> ThumbnailFiles { get; }

        // Methods.
        public long GetTotalByteSize() =>
            EncodedFiles.Sum(f => f.ByteSize) +
            ThumbnailFiles.Sum(t => t.ByteSize) +
            JsonSerializer.Serialize(new ManifestDto(this, "0000000000000000000000000000000000000000000000000000000000000000", "0x0000000000000000000000000000000000000000")).Length;
    }
}
