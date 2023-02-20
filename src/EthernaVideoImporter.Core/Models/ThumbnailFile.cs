namespace Etherna.VideoImporter.Core.Models
{
    public class ThumbnailFile : FileBase
    {
        public ThumbnailFile(
            string downloadedFilePath,
            long byteSize,
            int width,
            int height)
            : base(downloadedFilePath, byteSize)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Canvas height (in pixels).
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Canvas area (width multiplied by height).
        /// </summary>
        public int Area => Width * Height;
    }
}
