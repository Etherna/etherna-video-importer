﻿using Etherna.VideoImporter.Core;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptions
    {
        public string FFProbeFolderPath { get; set; } = CommonConsts.DefaultFFmpegFolder;
        public string FFProbeBinaryPath => Path.Combine(FFProbeFolderPath, CommonConsts.FFProbeBinaryName);
        public bool GenerateThumbnailWhenMissing { get; set; }
        public string JsonMetadataFilePath { get; set; } = default!;
    }
}
