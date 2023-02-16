using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models
{
    public class Video
    {
        // Fields.
        private readonly List<EncodedVideoFile> _encodedVideoFiles = new();

        // Constructor.
        public Video(VideoMetadataBase metadata)
        {
            Metadata = metadata;
        }

        // Properties.
        public string? DownloadedThumbnailPath { get; set; }
        public IEnumerable<EncodedVideoFile> EncodedVideoFiles => _encodedVideoFiles;
        public string? EthernaIndexUrl => EthernaIndexId is null ? null : CommonConst.PREFIX_ETHERNA_INDEX + EthernaIndexId;
        public string? EthernaPermalinkUrl => EthernaPermalinkId is null ? null : CommonConst.PREFIX_ETHERNA_PERMALINK + EthernaPermalinkId;
        public string? EthernaIndexId { get; set; }
        public string? EthernaPermalinkId { get; set; }
        public VideoMetadataBase Metadata { get; }
        public string? UploadedThumbnailReference { get; set; }

        // Methods.
        public void AddEncodedFile(EncodedVideoFile stream) => _encodedVideoFiles.Add(stream);
    }
}
