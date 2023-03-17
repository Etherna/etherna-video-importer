using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class AudioSwarmFile : SwarmFileBase
    {
        // Constructor.
        public AudioSwarmFile(
            long byteSize,
            string hash)
            : base(hash, byteSize)
        {
        }

        // Properties.
    }
}
