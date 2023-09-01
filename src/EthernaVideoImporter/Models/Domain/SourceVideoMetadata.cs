using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class SourceVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SourceVideoMetadata(
            string id,
            string title,
            string description,
            IEnumerable<string>? oldIds,
            VideoSourceFile sourceVideo,
            ThumbnailSourceFile? sourceThumbnail)
            : base(title, description, sourceVideo.Duration, sourceVideo.VideoQualityLabel)
        {
            Id = id;
            OldIds = oldIds ?? Array.Empty<string>();
            SourceThumbnail = sourceThumbnail;
            SourceVideo = sourceVideo;
        }

        // Properties.
        public override string Id { get; }
        public override IEnumerable<string> OldIds { get; }
        public ThumbnailSourceFile? SourceThumbnail { get; }
        public VideoSourceFile SourceVideo { get; }
    }
}
