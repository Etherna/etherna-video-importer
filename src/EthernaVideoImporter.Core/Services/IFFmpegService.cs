using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.FFmpegDto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IFFmpegService
    {
        // Methods.
        Task<IEnumerable<(string filePath, int height, int width)>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile,
            IEnumerable<int> outputHeights);
        Task<string> GetFFmpegBinaryPathAsync();
        Task<string> GetFFprobeBinaryPathAsync();
        Task<FFProbeResultDto> GetVideoInfoAsync(string videoFileAbsoluteUri);
    }
}