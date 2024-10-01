// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

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
