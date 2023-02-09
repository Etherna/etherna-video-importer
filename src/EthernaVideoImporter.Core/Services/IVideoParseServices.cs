using Etherna.EthernaVideoImporterLibrary.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    public interface IVideoParseServices
    {
        Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string uri);
        Task<VideoData?> ToVideoDataDtosAsync(string uri);
    }
}
