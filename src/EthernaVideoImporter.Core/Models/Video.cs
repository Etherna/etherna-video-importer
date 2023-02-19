using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models
{
    public class Video
    {
        // Constructor.
        public Video(
            VideoMetadataBase metadata,
            IEnumerable<EncodedFileBase> encodedFiles,
            string downloadedThumbnailPath)
        {
            EncodedFiles = encodedFiles;
            DownloadedThumbnailPath = downloadedThumbnailPath;
            Metadata = metadata;
        }

        // Properties.
        public string DownloadedThumbnailPath { get; }
        public IEnumerable<EncodedFileBase> EncodedFiles { get; }
        public string? EthernaIndexId { get; set; }
        public string? EthernaPermalinkHash { get; set; }
        public VideoMetadataBase Metadata { get; }
        public string? UploadedThumbnailReference { get; set; }
    }
}
