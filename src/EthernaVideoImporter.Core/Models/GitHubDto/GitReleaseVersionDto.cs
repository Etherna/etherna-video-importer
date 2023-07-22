using System;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Models.GitHubDto
{
    public class GitReleaseVersionDto
    {
        // Properties.
        public string Assets_url { get; set; } = default!;
        public IEnumerable<Asset> Assets { get; set; } = default!;
        public DateTime Created_at { get; set; }
        public bool Draft { get; set; }
        public string Html_url { get; set; } = default!;
        public int Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public bool Prerelease { get; set; }
        public DateTime Published_at { get; set; }
        public string Tag_name { get; set; } = default!;
        public string Url { get; set; } = default!;

        public class Asset
        {
            public string Name { get; set; } = default!;
            public string Browser_download_url { get; set; } = default!;
        }
    }
}
