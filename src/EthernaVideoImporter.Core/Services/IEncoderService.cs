using Etherna.VideoImporter.Core.Models.Domain;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEncoderService
    {
        Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile originalVideoLocalFile,
            AudioLocalFile originalAudioLocalFile,
            DirectoryInfo importerTempDirectoryInfo);
    }
}
