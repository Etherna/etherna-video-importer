using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class RemoteFile : FileBase
    {
        // Constructor.
        protected RemoteFile(
            Uri Uri)
        {
            this.Uri = Uri;
        }

        // Properties.
        public Uri Uri { get; }
    }
}
