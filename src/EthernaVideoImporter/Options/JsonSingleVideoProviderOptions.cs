using Etherna.VideoImporter.Core;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonSingleVideoProviderOptions
    {
        public string FFProbeFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string FFProbeBinaryPath => Path.Combine(FFProbeFolderPath, CommonConsts.FFProbeBinaryName);
        public string SourceUri { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string? Thumbnail { get; set; } 
    }
}
