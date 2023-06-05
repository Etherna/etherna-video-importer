using System.Collections.Generic;
using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    public sealed class EncoderServiceOptions
    {
        // Properties.
        public string FFMpegBinaryPath => Path.Combine(FFMpegFolderPath, CommonConsts.FFMpegBinaryName);
        public string FFMpegFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public bool IncludeAudioTrack { get; set; }
        public bool Skip360 { get; set; }
        public bool Skip480 { get; set; }
        public bool Skip720 { get; set; }
        public bool Skip1080 { get; set; }
        public bool Skip1440 { get; set; }

        // Methods.
        public IEnumerable<int> GetSupportedHeightResolutions()
        {
            var supportedHeightResolutions = new List<int>();
            if (!Skip1440)
                supportedHeightResolutions.Add(1440);
            if (!Skip1080)
                supportedHeightResolutions.Add(1080);
            if (!Skip720)
                supportedHeightResolutions.Add(720);
            if (!Skip480)
                supportedHeightResolutions.Add(480);
            if (!Skip360)
                supportedHeightResolutions.Add(360);

            return supportedHeightResolutions;
        }
    }
}
