using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class LocalFile : FileBase
    {
        // Constructor.
        public LocalFile(
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
