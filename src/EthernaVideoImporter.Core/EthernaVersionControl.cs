using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core
{
    public static class EthernaVersionControl
    {
        // Public methods.
        public static async Task<bool> CheckNewVersionAsync()
        {
            Version? currentVersion;
            Console.Write("Etherna Video Importer");

            // Get current version.
            try
            {
                var gitVersion = Assembly
                    .GetExecutingAssembly()?
                    .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                    .Version;
                currentVersion = !string.IsNullOrWhiteSpace(gitVersion) ? new Version(gitVersion) : null;
                if (currentVersion is not null)
                    Console.Write($" (v{currentVersion})");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Unable to check current version");
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                currentVersion = null;
            }
            Console.WriteLine();
            Console.WriteLine();

            // Get last version.
            try
            {
                using var httpClientGit = new HttpClient();
                httpClientGit.DefaultRequestHeaders.Add("User-Agent", "EthernaImportClient");
                var gitUrl = "https://api.github.com/repos/Etherna/etherna-video-importer/releases";
                var response = await httpClientGit.GetAsync(gitUrl);
                var gitReleaseVersionsDto = await response.Content.ReadFromJsonAsync<List<GitReleaseVersionDto>>();

                if (gitReleaseVersionsDto is null ||
                    !gitReleaseVersionsDto.Any())
                {
                    throw new InvalidOperationException("GitHub haven't any version");
                }

                var lastVersion = gitReleaseVersionsDto
                    .Select(git => new
                    {
                        Version = new Version(git.Tag_name.Replace("v", "", StringComparison.OrdinalIgnoreCase)),
                        Url = git.Html_url
                    })
                    .OrderByDescending(v => v.Version)
                    .First();

                if (currentVersion is not null &&
                    lastVersion.Version > currentVersion)
                {
                    Console.WriteLine($"A new release is available {lastVersion.Version}\nUpgrade now, or check out the release page at:\n {lastVersion.Url}");
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
