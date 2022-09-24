using System.Collections.Generic;

namespace Etherna.EthernaVideoImporter.Models
{
    public class SwarmImageRaw
    {
        // Constructors.
        public SwarmImageRaw(
            float aspectRatio,
            string blurhash,
            IReadOnlyDictionary<string, string> sources,
            string v)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Sources = sources;
            V = v;
        }

        // Properties.
        public float AspectRatio { get; }
        public string Blurhash { get; }
        public IReadOnlyDictionary<string, string> Sources { get; }
        public string V { get; }
    }
}
