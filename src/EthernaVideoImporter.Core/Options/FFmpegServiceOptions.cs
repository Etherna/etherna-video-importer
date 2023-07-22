using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Options
{
    public class FFmpegServiceOptions
    {
        // Consts.
        public static readonly string DefaultPresetCodec = "medium";
        public static readonly IEnumerable<string> PresetCodecs = new List<string> { "ultrafast", "superfast", "fast", "medium", "slow", "slower" };

        // Properties.
        public string FFmpegFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string PresetCodec { get; set; } = DefaultPresetCodec;
    }
}
