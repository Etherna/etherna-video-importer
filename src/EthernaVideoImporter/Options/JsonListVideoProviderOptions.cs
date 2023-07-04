using Etherna.VideoImporter.Core;
using Etherna.VideoImporter.Core.Models.Domain;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptions
    {
        public string FFProbeFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string FFProbeBinaryPath => Path.Combine(FFProbeFolderPath, CommonConsts.FFProbeBinaryName);
        public UriResource JsonMetadataResource { get; set; } = default!;
    }
}
