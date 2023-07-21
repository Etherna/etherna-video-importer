using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class SwarmFileBase : FileBase
    {
        // Fields.
        private readonly long byteSize;

        // Constructor.
        protected SwarmFileBase(
            string hash,
            long byteSize)
        {
            SwarmHash = hash;
            this.byteSize = byteSize;
        }

        // Methods.
        public override Task<long> GetByteSizeAsync() => Task.FromResult(byteSize);
    }
}
