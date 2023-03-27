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
        // Public methods.
        public static async Task<bool> CheckNewVersionAsync(HttpClient httpClient)
        {
            if (httpClient is null)
                throw new ArgumentNullException(nameof(httpClient));

            // Get current version.
            var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
                throw new InvalidOperationException("Invalid assembly version");
            var currentVersion = new Version(assemblyVersion);

            Console.WriteLine();
            Console.WriteLine($"Etherna Video Importer (v{currentVersion})");
            Console.WriteLine();

            // Get last version form github releases.
            try
            {
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

                if (lastVersion.Version > currentVersion)
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
