using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    internal sealed class SwarmVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SwarmVideoMetadata(
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            ManifestThumbnailDto? thumbnail,
            string title,
            string swarmUrl,
            string videoId)
            : base(description, duration, originVideoQualityLabel, title)
        {
            SwarmUrl = swarmUrl;
            Thumbnail = thumbnail;
            VideoId = videoId;
        }

        // Properties.
        public override string Id => SwarmUrl;
        public string SwarmUrl { get; }
        public ManifestThumbnailDto? Thumbnail { get; }
        public string VideoId { get; }
    }
}
