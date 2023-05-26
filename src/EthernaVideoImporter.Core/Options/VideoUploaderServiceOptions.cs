using System;

namespace Etherna.VideoImporter.Core.Options
{
    public sealed class VideoUploaderServiceOptions
    {
        // Consts.
        public static readonly TimeSpan DefaultTtlPostageStamp = TimeSpan.FromDays(365);

        // Properties.
        public bool AcceptPurchaseOfAllBatches { get; set; }
        public TimeSpan TtlPostageStamp { get; set; } = DefaultTtlPostageStamp;
        public string UserEthAddr { get; set; } = default!;
    }
}
