using System.Collections.Generic;

namespace Etherna.EthernaVideoImporter.Models
{
    public class SwarmImageRaw
    {
        // Constructors.
        public SwarmImageRaw(
            float aspectRatio,
            string blurhash,
            IReadOnlyDictionary<string, string> sources)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Sources = sources;
        }

        // Properties.
        public float AspectRatio { get; }
        public string Blurhash { get; }
        public IReadOnlyDictionary<string, string> Sources { get; }
    }
}
