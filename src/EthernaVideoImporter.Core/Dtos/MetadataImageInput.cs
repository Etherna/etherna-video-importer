using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Dtos
{
    public sealed class MetadataImageInput
    {
        // Constructors.
        public MetadataImageInput(
            float aspectRatio,
            string blurhash,
            IDictionary<string, string> sources)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Sources = sources;
        }

        // Properties.
        public float AspectRatio { get; set; }
        public string Blurhash { get; set; }
        public IDictionary<string, string> Sources { get; }
        public string V { get; } = "1.0";
    }
}
