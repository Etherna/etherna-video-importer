using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class ThumbnailSwarmFile : SwarmFileBase
    {
        // Constructor.
        public ThumbnailSwarmFile(
            float aspectRatio,
            string blurhash,
            string resolution,
            string hash,
            long? byteSize)
            : base(hash, byteSize)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Resolution = resolution;
        }

        // Properties.
        public float AspectRatio { get; set; }
        public string Blurhash { get; set; }
        public string Resolution { get; set; }
    }
}
