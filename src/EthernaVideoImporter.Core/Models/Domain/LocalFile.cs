using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class LocalFile : FileBase
    {
        // Constructor.
        protected LocalFile(
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
