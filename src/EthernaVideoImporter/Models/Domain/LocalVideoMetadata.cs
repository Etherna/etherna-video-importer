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
            ThumbnailSourceFile? sourceThumbnail,
            VideoSourceFile sourceVideo)
            : base(title, description, sourceVideo.Duration, $"{sourceVideo.Height}p")
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
