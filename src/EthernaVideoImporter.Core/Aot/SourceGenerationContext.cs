// Copyright 2022-present Etherna SA
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

using Etherna.VideoImporter.Core.Models.FFmpegDto;
using Etherna.VideoImporter.Core.Models.GitHubDto;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.VideoImporter.Core.Aot
{
    [JsonSerializable(typeof(FFProbeResultDto))]
    [JsonSerializable(typeof(List<GitReleaseVersionDto>))]
    [JsonSerializable(typeof(ManifestDto))]
    [JsonSerializable(typeof(ManifestPersonalDataDto))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal sealed partial class SourceGenerationContext : JsonSerializerContext
    {

    }
}