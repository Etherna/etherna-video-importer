using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    public sealed class EncoderServiceOptions
    {
        // Properties.
        public string FFMpegBinaryPath => Path.Combine(FFMpegFolderPath, CommonConsts.FFMpegBinaryName);
        public string FFMpegFolderPath { get; set; } = default!;
        public bool IncludeAudioTrack { get; set; }
    }
}
