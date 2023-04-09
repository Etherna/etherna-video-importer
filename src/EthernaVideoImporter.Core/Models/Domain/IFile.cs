namespace Etherna.VideoImporter.Core.Models.Domain
{
    public interface IFile
    {
        long? ByteSize { get; }
        string? SwarmHash { get; }
    }
}
