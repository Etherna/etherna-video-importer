// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.VideoImporter.Core.Models.GitHubDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class AppVersionService : IAppVersionService
    {
        // Fields.
        private Version? _currentVersion;

        // Properties.
        public Version CurrentVersion
        {
            get
            {
                if (_currentVersion is null)
                {
                    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
                        throw new InvalidOperationException("Invalid assembly version");
                    _currentVersion = new Version(assemblyVersion);
                }
                return _currentVersion;
            }
        }

        // Methods.
        public async Task<(Version version, string url)> GetLastVersionAsync()
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "EthernaImportClient");
            var gitUrl = "https://api.github.com/repos/Etherna/etherna-video-importer/releases";
            var response = await httpClient.GetAsync(gitUrl);
            var gitReleaseVersionsDto = await response.Content.ReadFromJsonAsync<List<GitReleaseVersionDto>>();

            if (gitReleaseVersionsDto is null || gitReleaseVersionsDto.Count == 0)
                throw new InvalidOperationException();

            return gitReleaseVersionsDto
                .Select(git => 
                (
                    version: new Version(git.Tag_name.Replace("v", "", StringComparison.OrdinalIgnoreCase)),
                    url: git.Html_url
                )).
                MaxBy(pair => pair.version);
        }
    }
}
