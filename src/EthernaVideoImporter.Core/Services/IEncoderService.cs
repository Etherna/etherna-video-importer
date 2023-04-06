using Etherna.VideoImporter.Core.Models.Domain;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEncoderService
    {
        string FFMpegBinaryPath { get; }

        Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile originalVideoLocalFile,
            DirectoryInfo importerTempDirectoryInfo,
            IEnumerable<int> supportedHeightResolutions,
            bool includeAudioTrack);
    }
}
