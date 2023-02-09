using Etherna.EthernaVideoImporterLibrary.Models;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    /// <summary>
    /// Downloader services
    /// </summary>
    public interface IVideoDownloaderService
    {
        /// <summary>
        /// Start download from youtube url.
        /// </summary>
        /// <param name="videoData">video data</param>
        Task<VideoData> StartDownloadAsync(VideoData videoData);
    }
}
