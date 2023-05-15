namespace Etherna.VideoImporter.Models.LocalVideoDto
{
    internal sealed class LocalVideoMetadataDto
    {
        // Properties.
        public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string VideoFilePath { get; set; } = default!;
        public string? ThumbnailFilePath { get; set; }
    }
}
