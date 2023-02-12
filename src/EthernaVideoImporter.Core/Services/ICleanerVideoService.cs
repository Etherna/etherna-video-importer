using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface ICleanerVideoService
    {
        Task RunCleanerAsync(
            IEnumerable<VideoData> allVideoDataInfos,
            IEnumerable<VideoDto> importedVideos);

        Task RunOldDeleterAsync(
            IEnumerable<VideoDto> videos);
    }
}