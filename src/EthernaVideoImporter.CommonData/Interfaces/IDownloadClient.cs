using EthernaVideoImporter.CommonData.Models;
using System;
using System.Threading.Tasks;

namespace EthernaVideoImporter.CommonData.Services
{
    public interface IDownloadClient
    {
        Task DownloadAsync(Uri uri, string filePath, IProgress<Tuple<long, long>> progress);
        Task<SourceVideoInfo> FirstVideoWithBestResolutionAsync(string url, int? maxFilesize);
    }
}
