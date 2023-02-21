using Etherna.VideoImporter.Core.Models;
using System;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Models
{
    internal sealed class YouTubeVideoMetadata : YouTubeVideoMetadataBase
    {
        // Constructors.
        internal YouTubeVideoMetadata(
            string description,
            TimeSpan duration,
            Thumbnail? thumbnail,
            string title,
            string youtubeUrl)
            : base(description, duration, thumbnail, title, youtubeUrl)
        { }

        // Properties.
        public override string Id => YoutubeId;
    }
}
