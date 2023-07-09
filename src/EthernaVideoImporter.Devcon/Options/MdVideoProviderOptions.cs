namespace Etherna.VideoImporter.Devcon.Options
{
    internal sealed class MdVideoProviderOptions
    {
        public bool GenerateThumbnailWhenMissing { get; set; }
        public string MdSourceFolderPath { get; set; } = default!;
    }
}
