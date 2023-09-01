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

namespace Etherna.VideoImporter.Core.Models.GitHubDto
{
    public class GitReleaseVersionDto
    {
        // Properties.
        public string Assets_url { get; set; } = default!;
        public DateTime Created_at { get; set; }
        public bool Draft { get; set; }
        public string Html_url { get; set; } = default!;
        public int Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public bool Prerelease { get; set; }
        public DateTime Published_at { get; set; }
        public string Tag_name { get; set; } = default!;
        public string Url { get; set; } = default!;
    }
}
