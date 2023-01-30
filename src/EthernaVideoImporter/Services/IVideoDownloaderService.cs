using Etherna.EthernaVideoImporter.Models;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    /// <summary>
    /// Downloader services
    /// </summary>
    internal interface IVideoDownloaderService
    {
        /// <summary>
        /// Start download from youtube url.
        /// </summary>
        /// <param name="videoData">video data</param>
        Task<VideoData> StartDownloadAsync(VideoData videoData);
    }
}
