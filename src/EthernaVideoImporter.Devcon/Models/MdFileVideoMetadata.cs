using Etherna.VideoImporter.Core.Models;
using System;
using System.Linq;
using System.Web;

namespace Etherna.VideoImporter.Devcon.Models
{
    public class MdFileVideoMetadata : VideoMetadataBase
    {
        public MdFileVideoMetadata(
            string description,
            string title,
            string mdFilePath,
            string youtubeUrl)
            : base(description, title)
        {
            MdFilePath = mdFilePath;
            YoutubeUrl = youtubeUrl;
        }

        // Properties.
        public override string Id => MdFilePath;
        public string MdFilePath { get; set; }
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
