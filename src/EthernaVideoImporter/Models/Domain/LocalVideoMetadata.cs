using Etherna.VideoImporter.Core.Models.Domain;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class LocalVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal LocalVideoMetadata(
            string id,
            string title,
            string description,
            VideoSourceFile sourceVideo,
            ThumbnailSourceFile? sourceThumbnail)
            : base(title, description, sourceVideo.Duration, sourceVideo.VideoQualityLabel)
        {
            Id = id;
            SourceThumbnail = sourceThumbnail;
            SourceVideo = sourceVideo;
        }

        // Properties.
        public override string Id { get; }
        public ThumbnailSourceFile? SourceThumbnail { get; }
        public VideoSourceFile SourceVideo { get; }
    }
}
