using Etherna.EthernaVideoImporter.Dtos;

namespace Etherna.EthernaVideoImporter.Services
{
    public interface IDownloadClient
    {
        Task DownloadAsync(Uri uri, string filePath, IProgress<Tuple<long, long>> progress);
        Task<SourceVideoInfo> FirstVideoWithBestResolutionAsync(string url);
    }
}
