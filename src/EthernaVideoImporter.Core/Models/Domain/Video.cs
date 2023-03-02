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
            ThumbnailFile? thumbnailFile)
        {
            if (!encodedFiles.Any())
                throw new ArgumentException("Must exist at least a stream");

            EncodedFiles = encodedFiles;
            Metadata = metadata;
            ThumbnailFile = thumbnailFile;
        }

        // Properties.
        public IEnumerable<FileBase> EncodedFiles { get; }
        public string? EthernaIndexId { get; set; }
        public string? EthernaPermalinkHash { get; set; }
        public VideoMetadataBase Metadata { get; }
        public ThumbnailFile? ThumbnailFile { get; }

        // Methods.
        public long GetTotalByteSize() =>
            EncodedFiles.Sum(f => f.ByteSize) +
            ThumbnailFile?.ByteSize ?? 0 +
            JsonSerializer.Serialize(new ManifestDto(this, "0000000000000000000000000000000000000000000000000000000000000000", "0x0000000000000000000000000000000000000000")).Length;
    }
}
