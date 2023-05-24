using System;

namespace Etherna.VideoImporter.Core.Options
{
    public sealed class VideoUploaderServiceOptions
    {
        public bool AcceptPurchaseOfAllBatches { get; set; }
        public TimeSpan TtlPostageStamp { get; set; }
        public string UserEthAddr { get; set; } = default!;
    }
}
