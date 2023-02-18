using Etherna.VideoImporter.Core.Models;

namespace Etherna.VideoImporter.Devcon.Models
{
    public class MdFileVideoMetadata : VideoMetadataBase
    {
        // Constructor.
        public MdFileVideoMetadata(
            string description,
            string title,
            string mdFilePath,
            string youtubeUrl,
            string? ethernaIndexUrl,
            string? ethernaPermalinkUrl)
            : base(description, title)
        {
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFilePath = mdFilePath;
            YoutubeUrl = youtubeUrl;
        }

        // Properties.
        public override string Id => MdFilePath;
        public string? EthernaIndexUrl { get; set; }
        public string? EthernaPermalinkUrl { get; set; }
        public string MdFilePath { get; set; }
        public string YoutubeUrl { get; set; }
    }
}
