using System;
using System.Collections.Generic;

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
            IEnumerable<ThumbnailSwarmFile> thumbnails,
            string title)
            : base(description, duration, originVideoQualityLabel, title)
        {
            Thumbnails = thumbnails ?? new List<ThumbnailSwarmFile>();
            VideoId = videoId;
        }

        // Properties.
        public override string Id => VideoId;
        public IEnumerable<ThumbnailSwarmFile> Thumbnails { get; } 
        public string VideoId { get; }
    }
}
