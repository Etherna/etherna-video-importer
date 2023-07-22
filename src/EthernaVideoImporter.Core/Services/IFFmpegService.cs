using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.FFmpegDto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IFFmpegService
    {
        // Properties.
        string FFmpegBinaryPath { get; }
        string FFprobeBinaryPath { get; }

        // Methods.
        Task<IEnumerable<(string filePath, int height, int width)>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile,
            IEnumerable<int> outputHeights);

        FFProbeResultDto GetVideoInfo(string videoFileAbsoluteUri);
    }
}