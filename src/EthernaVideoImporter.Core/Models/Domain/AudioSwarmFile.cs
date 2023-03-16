using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class AudioSwarmFile : SwarmFileBase
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
