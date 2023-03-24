namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class ThumbnailSwarmFile : SwarmFileBase, IThumbnailFile
    {
        // Constructor.
        public ThumbnailSwarmFile(
            float aspectRatio,
            string blurhash,
            string hash,
            long byteSize,
            int width)
            : base(hash, byteSize)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Width = width;
        }

        // Properties.
        public float AspectRatio { get; }
        public string Blurhash { get; }
        public int Height => (int)(Width / AspectRatio);
        public int Width { get; }
    }
}
