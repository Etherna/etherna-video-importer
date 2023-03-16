using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class AudioSwarmFile : RemoteFile
    {
        // Constructor.
        public AudioSwarmFile(
            long byteSize,
            Uri uri)
            : base(uri)
        {
            ByteSize = byteSize;
        }

        // Properties.
        public long ByteSize { get; }
    }
}
