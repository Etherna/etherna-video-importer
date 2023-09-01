using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.SourceVideoDtos
{
    internal sealed class JsonVideoMetadataDto
    {
        // Properties.
        public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string VideoFilePath { get; set; } = default!;
        public string? ThumbnailFilePath { get; set; }
        public IEnumerable<string>? OldIds { get; set; }
    }
}
