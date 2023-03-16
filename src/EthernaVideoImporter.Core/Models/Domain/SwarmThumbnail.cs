using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class SwarmThumbnail : RemoteFile
    {
        // Constructor.
        public SwarmThumbnail(
            float aspectRatio,
            string blurhash,
            string resolution,
            Uri uri)
            : base(uri)
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
