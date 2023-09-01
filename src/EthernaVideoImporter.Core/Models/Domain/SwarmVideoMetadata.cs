using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    internal sealed class SwarmVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal SwarmVideoMetadata(
            string id,
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel)
            : base(title, description, duration, originVideoQualityLabel)
        {
            Id = id;
        }

        // Properties.
        public override string Id { get; }
        public override IEnumerable<string> OldIds => Array.Empty<string>();
    }
}
