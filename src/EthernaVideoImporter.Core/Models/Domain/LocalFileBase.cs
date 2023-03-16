using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class LocalFileBase : FileBase
    {
        // Constructor.
        protected LocalFileBase(
            string downloadedFilePath,
            long byteSize)
        {
            ByteSize = byteSize;
            DownloadedFilePath = downloadedFilePath;
        }

        // Properties.
        public long ByteSize { get; }
        public string DownloadedFilePath { get; }
    }
}
