using System;
using System.IO;

namespace Etherna.VideoImporter.Core
{
    public class ImporterSettings
    {
        public bool AcceptPurchaseOfAllBatches { get; set; }
        public bool DeleteExogenousVideos { get; set; }
        public bool DeleteVideosMissingFromSource { get; set; }
        public string FFMpegBinaryPath { get; set; } = default!;
        public string FFMpegFolderPath { get; set; } = default!;
        public bool ForceUploadVideo { get; set; }
        public bool IgnoreNewVersionsOfImporter { get; set; }
        public bool IncludeAudioTrack { get; set; }
        public bool OfferVideos { get; set; }
        public bool PinVideos { get; set; }
        public string SourceUri { get; set; } = default!;
        public bool Skip360 { get; set; }
        public bool Skip480 { get; set; }
        public bool Skip720 { get; set; }
        public bool Skip1080 { get; set; }
        public bool Skip1440 { get; set; }
        public DirectoryInfo TempDirectoryPath { get; set; } = default!;
        public TimeSpan TTLPostageStamp { get; set; }
        public bool UnpinRemovedVideos { get; set; }
        public string UserEthAddr { get; set; } = default!;
    }
}
