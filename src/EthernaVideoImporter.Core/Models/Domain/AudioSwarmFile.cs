namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class AudioSwarmFile : SwarmFileBase
    {
        // Constructor.
        public AudioSwarmFile(
            string hash,
            long byteSize)
            : base(hash, byteSize)
        {
        }
    }
}
