using System;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class RemoteFile : FileBase
    {
        // Constructor.
        public RemoteFile(
            Uri Uri)
        {
            this.Uri = Uri;
        }

        // Properties.
        public Uri Uri { get; }
    }
}
