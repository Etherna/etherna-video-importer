using Etherna.VideoImporter.Core.Models;
using System;
using System.Linq;
using System.Web;
using YoutubeExplode.Common;

namespace Etherna.VideoImporter.Models
{
    internal sealed class YouTubeVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal YouTubeVideoMetadata(YoutubeExplode.Videos.Video youtubeMetadata)
            : this(youtubeMetadata.Description,
                  youtubeMetadata.Duration ?? throw new InvalidOperationException("Live streams are not supported"),
                  youtubeMetadata.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault(),
                  youtubeMetadata.Title,
                  youtubeMetadata.Url)
        { }

        internal YouTubeVideoMetadata(
            string description,
            TimeSpan duration,
            Thumbnail? thumbnail,
            string title,
            string youtubeUrl)
            : base(description, title)
        {
            Duration = duration;
            Thumbnail = thumbnail;
            YoutubeUrl = youtubeUrl;
        }

        // Properties.
        public override string Id => YoutubeId;
        public override TimeSpan Duration { get; }
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
        public string YoutubeUrl { get; set; }
    }
}
