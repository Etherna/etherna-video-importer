using Etherna.VideoImporter.Core.Models.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEncoderService
    {
        string FFMpegBinaryPath { get; }

        Task<string> CreateRandomThumbnailAsync(
            string absoluteVideoFilePath);

        Task<IEnumerable<VideoLocalFile>> EncodeVideosAsync(
            VideoLocalFile sourceVideoFile);
    }
}
