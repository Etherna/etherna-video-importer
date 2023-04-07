using Etherna.VideoImporter.Core.Models.Domain;
using System;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class LocalVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal LocalVideoMetadata(
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            ThumbnailLocalFile? thumbnail,
            string title,
            string filePathVideo)
            : base(description, duration, originVideoQualityLabel, title)
        {
            FilePath = filePathVideo;
            Thumbnail = thumbnail;
        }

        // Properties.
        public override string Id => FilePath;
        public string FilePath { get; }
        public ThumbnailLocalFile? Thumbnail { get; }
    }
}
