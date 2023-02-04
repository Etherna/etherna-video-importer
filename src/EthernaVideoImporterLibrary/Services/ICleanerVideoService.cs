using Etherna.EthernaVideoImporterLibrary.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    public interface ICleanerVideoService
    {
        Task RunCleanerAsync(IEnumerable<VideoData> allVideoDataInfos);
        Task RunOldDeleterAsync();
    }
}