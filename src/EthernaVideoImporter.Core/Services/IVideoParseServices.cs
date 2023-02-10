using Etherna.VideoImporter.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IVideoParseServices
    {
        Task<IEnumerable<VideoDataMinimalInfo>> ToVideoDataMinimalInfoDtosAsync(string uri);
        Task<VideoData?> ToVideoDataDtosAsync(string uri);
    }
}
