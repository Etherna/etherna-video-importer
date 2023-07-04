﻿using Etherna.VideoImporter.Core.Models.Domain;
using System;

namespace Etherna.VideoImporter.Models.Domain
{
    internal sealed class LocalVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal LocalVideoMetadata(
            string id,
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            ThumbnailSourceFile? sourceThumbnail,
            VideoSourceFile sourceVideo)
            : base(title, description, duration, originVideoQualityLabel)
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
