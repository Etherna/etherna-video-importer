namespace Etherna.VideoImporter.Options
{
    internal sealed class YouTubeSingleVideoProviderOptions
    {
        public bool GenerateThumbnailWhenMissing { get; set; }
        public string VideoUrl { get; set; } = default!;
    }
}
