using Etherna.VideoImporter.Core.Models.Domain;
using System;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class YouTubeVideoMetadata : YouTubeVideoMetadataBase
    {
        // Constructors.
        internal YouTubeVideoMetadata(
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            Thumbnail? thumbnail,
            string title,
            string youtubeUrl)
            : base(description, duration, originVideoQualityLabel, thumbnail, title, youtubeUrl)
        { }

        // Properties.
        public override string Id => YoutubeId;
    }
}
