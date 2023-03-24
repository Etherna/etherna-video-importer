using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class LocalFileBase : FileBase
    {
        // Constructor.
        protected LocalFileBase(
            string filePath,
            long byteSize)
            : base(byteSize)
        {
            FilePath = filePath;
        }

        // Properties.
        public string FilePath { get; }

        // Methods.
        public void SetSwarmHash(string swarmHash)
        {
            if (SwarmHash != null)
                throw new InvalidOperationException("Swarm hash already set");
            SwarmHash = swarmHash;
        }
    }
}
