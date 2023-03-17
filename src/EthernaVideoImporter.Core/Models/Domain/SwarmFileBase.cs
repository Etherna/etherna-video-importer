namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class SwarmFileBase : FileBase
    {
        // Constructor.
        protected SwarmFileBase(
            string hash,
            long? byteSize)
            : base(byteSize)
        {
            this.Hash = hash;
        }

        // Properties.
        public string Hash { get; }
    }
}
