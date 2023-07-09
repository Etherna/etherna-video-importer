namespace Etherna.VideoImporter.Options
{
    internal sealed class YouTubeChannelVideoProviderOptions
    {
        public string ChannelUrl { get; set; } = default!;
        public bool GenerateThumbnailWhenMissing { get; set; }
    }
}
