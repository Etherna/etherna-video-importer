namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class LocalFileBase : FileBase
    {
        // Constructor.
        protected LocalFileBase(
            string downloadedFilePath,
            long byteSize)
            : base(byteSize)
        {
            DownloadedFilePath = downloadedFilePath;
        }

        // Properties.
        public string DownloadedFilePath { get; }
    }
}
