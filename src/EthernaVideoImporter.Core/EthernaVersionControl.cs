//   Copyright 2022-present Etherna SA
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Etherna.VideoImporter.Core.Models.GitHubDto;

namespace Etherna.VideoImporter.Core
{
    public static class EthernaVersionControl
    {
        // Fields.
        private static Version? _currentVersion;

        // Properties.
        public static Version CurrentVersion
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

        // Public methods.
        public static async Task<bool> CheckNewVersionAsync()
        {
            // Get current version.
            
            Console.WriteLine();
            Console.WriteLine($"Etherna Video Importer (v{CurrentVersion})");
            Console.WriteLine();

            // Get last version form github releases.
            try
            {
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "EthernaImportClient");
                var gitUrl = "https://api.github.com/repos/Etherna/etherna-video-importer/releases";
                var response = await httpClient.GetAsync(gitUrl);
                var gitReleaseVersionsDto = await response.Content.ReadFromJsonAsync<List<GitReleaseVersionDto>>();

                if (gitReleaseVersionsDto is null || !gitReleaseVersionsDto.Any())
                    return false;

                var lastVersion = gitReleaseVersionsDto
                    .Select(git => new
                    {
                        Version = new Version(git.Tag_name.Replace("v", "", StringComparison.OrdinalIgnoreCase)),
                        Url = git.Html_url
                    })
                    .OrderByDescending(v => v.Version)
                    .First();

                if (lastVersion.Version > CurrentVersion)
                {
                    Console.WriteLine($"A new release is available: {lastVersion.Version}");
                    Console.WriteLine($"Upgrade now, or check out the release page at:");
                    Console.WriteLine($" {lastVersion.Url}");
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Unable to check last version on GitHub");
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }
    }
}
