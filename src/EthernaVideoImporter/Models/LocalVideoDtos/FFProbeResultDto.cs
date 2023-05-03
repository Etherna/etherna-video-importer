using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Models.LocalVideoDtos
{
    internal class FFProbeResultDto
    {
        // Classes.
        public class FormatResult
        {
            public TimeSpan Duration { get; set; }
        }

        public class StreamResult
        {
            public int Height { get; set; }
            public int Width { get; set; }
        }

        // Properties.
        public IEnumerable<StreamResult> Streams { get; set; } = default!;
        public FormatResult Format { get; set; } = default!;
    }
}
