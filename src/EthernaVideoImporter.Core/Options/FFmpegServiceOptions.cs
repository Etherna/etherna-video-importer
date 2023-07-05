using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    public class FFmpegServiceOptions
    {
        public string FFMpegBinaryPath => Path.Combine(FFMpegFolderPath, CommonConsts.FFMpegBinaryName);
        public string FFMpegFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string FFProbeBinaryPath => Path.Combine(FFMpegFolderPath, CommonConsts.FFProbeBinaryName);
    }
}
