using System;
using System.Linq;
using System.Web;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Core.Models
{
    public abstract class YouTubeVideoMetadataBase : VideoMetadataBase
    {
        protected YouTubeVideoMetadataBase(
            string description,
            TimeSpan duration,
            Thumbnail? thumbnail,
            string title,
            string youtubeUrl)
            : base(description, duration, title)
        {
            Thumbnail = thumbnail;
            YoutubeUrl = youtubeUrl;
        }

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
