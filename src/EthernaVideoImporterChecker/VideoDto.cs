using System;
using System.Linq;

namespace EthernaVideoImporterChecker
{
    public class VideoDto
    {
        // Properties.
        public string? Id { get; set; }
        public DateTime CreationDateTime { get; set; }
        public VideoManifestDto? LastValidManifest { get; set; }
        public string? OwnerAddress { get; set; }
        public long TotDownvotes { get; set; }
        public long TotUpvotes { get; set; }
    }
}
