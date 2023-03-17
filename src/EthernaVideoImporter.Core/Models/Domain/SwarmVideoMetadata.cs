using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    internal sealed class SwarmVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SwarmVideoMetadata(
            string videoId,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            ManifestThumbnailDto? thumbnail,
            string title)
            : base(description, duration, originVideoQualityLabel, title)
        {
            Thumbnail = thumbnail;
            VideoId = videoId;
        }

        // Properties.
        public override string Id => VideoId;
        public ManifestThumbnailDto? Thumbnail { get; } // DTO is used even in YoutubeManifest and MdManifest.
        public string VideoId { get; }
    }
}
