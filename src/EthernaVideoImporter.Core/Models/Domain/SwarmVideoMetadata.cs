using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using YoutubeExplode.Common;

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
            string swarmUrl)
            : base(description, duration, originVideoQualityLabel, title)
        {
            SwarmUrl = swarmUrl;
            Thumbnail = thumbnail;
        }

        // Properties.
        public override string Id => SwarmUrl;
        public string SwarmUrl { get; }
        public ManifestThumbnailDto? Thumbnail { get; }
    }
}
