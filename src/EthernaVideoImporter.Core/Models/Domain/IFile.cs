using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public interface IFile
    {
        // Properties.
        string? SwarmHash { get; }

        // Methods.
        Task<long> GetByteSizeAsync();
    }
}
