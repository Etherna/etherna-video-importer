using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.LocalVideoDtos
{
    internal sealed class FFProbeResultDto
    {
        // Classes.
        public sealed class FormatResult
        {
            // Properties.
            public TimeSpan Duration { get; set; }
            public string Size { get; set; } = default!;
            public long SizeLong => Convert.ToInt64(Size);
        }

        public sealed class StreamResult
        {
            // Properties.
            public int Height { get; set; }
            public int Width { get; set; }
        }

        // Properties.
        public IEnumerable<StreamResult> Streams { get; set; } = default!;
        public FormatResult Format { get; set; } = default!;
    }
}
