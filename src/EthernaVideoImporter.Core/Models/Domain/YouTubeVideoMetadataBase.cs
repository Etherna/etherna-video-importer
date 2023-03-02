using System;
using System.Linq;
using System.Web;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class YouTubeVideoMetadataBase : VideoMetadataBase
    {
        // Constructor.
        protected YouTubeVideoMetadataBase(
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            Thumbnail? thumbnail,
            string title,
            string youtubeUrl)
            : base(description, duration, originVideoQualityLabel, title)
        {
            Thumbnail = thumbnail;
            YoutubeUrl = youtubeUrl;
        }

        // Properties.
        public Thumbnail? Thumbnail { get; }
        public string YoutubeId
        {
            get
            {
                var uri = new Uri(YoutubeUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);

                if (query.AllKeys.Contains("v"))
                    return query["v"]!;

                return uri.Segments.Last();
            }
        }
        public string YoutubeUrl { get; }
    }
}
