using Etherna.VideoImporter.Core.Models;
using System;

namespace Etherna.VideoImporter.Devcon.Models
{
    public class MdFileVideoMetadata : VideoMetadataBase
    {
        private TimeSpan? _duration;

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
        public override TimeSpan Duration => _duration ?? throw new InvalidOperationException();
        public string? EthernaIndexUrl { get; set; }
        public string? EthernaPermalinkUrl { get; set; }
        public string MdFilePath { get; set; }
        public string YoutubeUrl { get; set; }

        // Methods.
        /*
         * This method is required because we doesn't take the duration value from Md files,
         * but it is taken instead directly from video streams.
         */
        public void SetDuration(TimeSpan duration) => _duration = duration;
    }
}
