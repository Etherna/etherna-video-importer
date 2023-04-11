using System;

namespace Etherna.VideoImporter.Core.Settings
{
    public class UploadSettings
    {
        public bool AcceptPurchaseOfAllBatches { get; set; }
        public bool IncludeAudioTrack { get; set; }
        public bool OfferVideos { get; set; }
        public bool PinVideos { get; set; }
        public bool Skip360 { get; set; }
        public bool Skip480 { get; set; }
        public bool Skip720 { get; set; }
        public bool Skip1080 { get; set; }
        public bool Skip1440 { get; set; }
        public TimeSpan TTLPostageStamp { get; set; }
    }
}
