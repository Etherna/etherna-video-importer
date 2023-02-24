using Etherna.VideoImporter.Core.Models;
using System;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Devcon.Models
{
    public class MdFileVideoMetadata : YouTubeVideoMetadataBase
    {
        // Constructor.
        public MdFileVideoMetadata(
            string description,
            TimeSpan duration,
            Thumbnail? thumbnail,
            string title,
            string mdFileRelativePath,
            string youtubeUrl,
            string? ethernaIndexUrl,
            string? ethernaPermalinkUrl)
            : base(description, duration, thumbnail, title, youtubeUrl)
        {
            EthernaIndexUrl = ethernaIndexUrl;
            EthernaPermalinkUrl = ethernaPermalinkUrl;
            MdFileRelativePath = mdFileRelativePath;
        }

        // Properties.
        public override string Id => MdFileRelativePath;
        public string? EthernaIndexUrl { get; }
        public string? EthernaPermalinkUrl { get; }
        public string MdFileRelativePath { get; }
    }
}
