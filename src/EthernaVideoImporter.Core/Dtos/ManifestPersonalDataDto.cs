﻿//   Copyright 2022-present Etherna Sagl
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

namespace Etherna.VideoImporter.Core.Dtos
{
    public class ManifestPersonalDataDto
    {
        public string? ClientName { get; set; }
        public string? VideoId { get; set; }

        public static ManifestPersonalDataDto BuildNew(string videoId) => new()
        {
            ClientName = "EthernaImporter",
            VideoId = videoId
        };
    }
}