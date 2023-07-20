using System;
using System.Collections.Generic;
using System.Globalization;

namespace Etherna.VideoImporter.Core.Models.FFmpegDto
{
    public sealed class FFProbeResultDto
    {
        // Classes.
        public sealed class FormatResult
        {
            // Properties.
            public TimeSpan Duration { get; set; }
            public string Size { get; set; } = default!;
            public long SizeLong => Convert.ToInt64(Size, CultureInfo.InvariantCulture);
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
