namespace Etherna.VideoImporter.Core.Models.Domain
{
    public interface IVideoFile : IFile
    {
        string VideoQualityLabel { get; }
    }
}
