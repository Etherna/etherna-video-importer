namespace Etherna.VideoImporter.Core.Models.Domain
{
    public interface IThumbnailFile : IFile
    {
        float AspectRatio { get; }
        string Blurhash { get; }
        int Height { get; }
        int Width { get; }
    }
}
