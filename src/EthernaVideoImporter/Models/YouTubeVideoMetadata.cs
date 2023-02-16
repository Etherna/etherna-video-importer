using Etherna.VideoImporter.Core.Models;
using System;
using System.Linq;
using System.Web;

namespace Etherna.VideoImporter.Models
{
    internal sealed class YouTubeVideoMetadata : VideoMetadataBase
    {
        // Constructors.
        internal YouTubeVideoMetadata(YoutubeExplode.Videos.Video youtubeMetadata)
            : this(youtubeMetadata.Description, youtubeMetadata.Title, youtubeMetadata.Url)
        { }

        internal YouTubeVideoMetadata(
            string description,
            string title,
            string youtubeUrl)
            : base(description, title)
        {
            YoutubeUrl = youtubeUrl;
        }

        // Properties.
        public override string Id => YoutubeId;
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
