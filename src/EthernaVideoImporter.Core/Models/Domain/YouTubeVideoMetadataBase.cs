//   Copyright 2022-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

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
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel,
            Thumbnail? thumbnail,
            string youtubeUrl)
            : base(title, description, duration, originVideoQualityLabel)
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
