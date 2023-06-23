using Etherna.VideoImporter.Core;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class LocalVideoProviderOptions
    {
        public string FFProbeFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string FFProbeBinaryPath => Path.Combine(FFProbeFolderPath, CommonConsts.FFProbeBinaryName);
        public string JsonMetadataFilePath { get; set; } = default!;
    }
}
