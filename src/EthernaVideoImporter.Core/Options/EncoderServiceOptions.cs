using System.Collections.Generic;
using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    public sealed class EncoderServiceOptions
    {
        // Consts.
        public static readonly string DefaultPresetCodec = "medium";
        public static readonly IList<string> PresetCodecs = new List<string> { "ultrafast", "superfast", "fast", "medium", "slow", "slower" };

        // Properties.
        public string FFMpegBinaryPath => Path.Combine(FFMpegFolderPath, CommonConsts.FFMpegBinaryName);
        public string FFMpegFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public bool IncludeAudioTrack { get; set; }
        public string PresetCodec { get; set; } = DefaultPresetCodec;
    }
}
