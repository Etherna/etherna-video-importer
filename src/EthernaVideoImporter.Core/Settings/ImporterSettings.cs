using System.IO;

namespace Etherna.VideoImporter.Core.Settings
{
    public class ImporterSettings
    {
        public bool DeleteExogenousVideos { get; set; }
        public bool DeleteVideosMissingFromSource { get; set; }
        public bool ForceUploadVideo { get; set; }
        public bool IgnoreNewVersionsOfImporter { get; set; }
        public string SourceUri { get; set; } = default!;
        public DirectoryInfo TempDirectoryPath { get; set; } = default!;
        public bool UnpinRemovedVideos { get; set; }
        public bool UseBeeNativeNode { get; set; }
        public string UserEthAddr { get; set; } = default!;
    }
}
