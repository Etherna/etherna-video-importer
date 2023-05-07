using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.LocalVideoDtos
{
    internal sealed class FFProbeResultDto
    {
        // Classes.
        public sealed class FormatResult
        {
            public TimeSpan Duration { get; set; }
        }

        public sealed class StreamResult
        {
            public int Height { get; set; }
            public int Width { get; set; }
        }

        // Properties.
        public IEnumerable<StreamResult> Streams { get; set; } = default!;
        public FormatResult Format { get; set; } = default!;
    }
}
