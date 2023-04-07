using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    internal sealed class SwarmVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SwarmVideoMetadata(
            string id,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            string title)
            : base(description, duration, originVideoQualityLabel, title)
        {
            Id = id;
            VideoIdHash = HashVideoId(id);
        }

        // Properties.
        public override string Id { get; }
        public override string VideoIdHash { get; }
    }
}
