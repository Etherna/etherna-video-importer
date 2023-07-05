using Etherna.VideoImporter.Core.Models.FFmpegDto;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IFFmpegService
    {
        FFProbeResultDto GetFFProbeVideoInfo(string videoFileAbsoluteUri);
    }
}