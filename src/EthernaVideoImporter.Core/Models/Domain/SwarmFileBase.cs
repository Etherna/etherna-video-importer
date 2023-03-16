using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class SwarmFileBase : FileBase
    {
        // Constructor.
        protected SwarmFileBase(
            Uri Uri)
        {
            this.Uri = Uri;
        }

        // Properties.
        public Uri Uri { get; }
    }
}
