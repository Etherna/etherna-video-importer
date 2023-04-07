namespace Etherna.VideoImporter.Models.LocalVideoDto
{
    internal sealed class ArchiveLocalVideoDto
    {
        // Properties.
        public string Description { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Thumbnail { get; set; }
        public string Video { get; set; } = default!;
    }
}
