using Etherna.VideoImporter.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface ICleanerVideoService
    {
        Task RunCleanerAsync(IEnumerable<VideoData> allVideoDataInfos);
        Task RunOldDeleterAsync();
    }
}