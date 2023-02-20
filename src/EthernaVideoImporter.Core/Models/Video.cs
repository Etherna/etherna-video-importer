using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models
{
    public class Video
    {
        // Constructor.
        public Video(
            VideoMetadataBase metadata,
            IEnumerable<FileBase> encodedFiles,
            ThumbnailFile? thumbnailFile)
        {
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
    }
}
