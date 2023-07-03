using Etherna.VideoImporter.Core.Models.GitHubDto;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Utilities
{
    public static class FFmpegUtility
    {
        // Methods.
        public static async Task<string?> CheckAndGetAsync(string? customFFMpegFolderPath)
        {
            // Custom FFmpeg folder.
            if (!string.IsNullOrWhiteSpace(customFFMpegFolderPath))
            {
                if (!Directory.Exists(customFFMpegFolderPath))
                    return null; // Not found.

                try
                {
                    var command = Command.Run($"{customFFMpegFolderPath}/{CommonConsts.FFMpegBinaryName}", "-version");
                    var result = await command.Task;
                    if (result.Success)
                        return customFFMpegFolderPath;
                }
                catch (System.ComponentModel.Win32Exception) { }
            }

            // Default FFmpeg folder.
            try
            {
                var command = Command.Run($"{CommonConsts.DefaultFFmpegFolder}/{CommonConsts.FFMpegBinaryName}", "-version");
                var result = await command.Task;
                if (result.Success)
                    return CommonConsts.DefaultFFmpegFolder;
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Global FFmpeg.
            try
            {
                var command = Command.Run(CommonConsts.FFMpegBinaryName, "-version");
                var result = await command.Task;
                if (result.Success)
                    return "";
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Not found.
            return null;
        }

        public static async Task<string> DownloadFFmpegAsync(bool ffmpeg, bool ffprobe)
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "EthernaImportClient");
            var gitUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
            var response = await httpClient.GetAsync(gitUrl);
            var gitReleaseVersionsDto = await response.Content.ReadFromJsonAsync<List<GitReleaseVersionDto>>();

            if (gitReleaseVersionsDto is null || !gitReleaseVersionsDto.Any())
                throw new InvalidOperationException($"Cannot find any version of FFmpeg in {gitUrl}");

            var gitReleaseVersionDto = gitReleaseVersionsDto
                    .Where(grv => grv.Tag_name == "latest")
                    .First();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return await WindowsDownloadFFmpegAsync(ffmpeg, ffprobe, gitReleaseVersionDto);
            else
                return await LinuxDownloadFFmpegAsync(ffmpeg, ffprobe, gitReleaseVersionDto);
        }

        public static async Task<string?> FFProbeCheckAndGetAsync(string? customFFMpegFolderPath)
        {
            // Custom FFmpeg folder.
            if (!string.IsNullOrWhiteSpace(customFFMpegFolderPath))
            {
                if (!Directory.Exists(customFFMpegFolderPath))
                    return null; // Not found.

                try
                {
                    var command = Command.Run($"{customFFMpegFolderPath}/{CommonConsts.FFProbeBinaryName}", "-version");
                    var result = await command.Task;
                    if (result.Success)
                        return customFFMpegFolderPath;
                }
                catch (System.ComponentModel.Win32Exception) { }
            }

            // Default FFmpeg folder.
            try
            {
                var command = Command.Run($"{CommonConsts.DefaultFFmpegFolder}/{CommonConsts.FFProbeBinaryName}", "-version");
                var result = await command.Task;
                if (result.Success)
                    return CommonConsts.DefaultFFmpegFolder;
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Global FFmpeg.
            try
            {
                var command = Command.Run(CommonConsts.FFProbeBinaryName, "-version");
                var result = await command.Task;
                if (result.Success)
                    return "";
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Not found.
            return null;
        }

        // Helpers.
        public static async Task<string> LinuxDownloadFFmpegAsync(bool ffmpeg, bool ffprobe, GitReleaseVersionDto gitReleaseVersionDto)
        {
            if (gitReleaseVersionDto is null)
                throw new ArgumentNullException(nameof(gitReleaseVersionDto));

            string? assetUrl = null;
            foreach (var asset in gitReleaseVersionDto.Assets)
            {
                if (!asset.Name.Contains("-linux64", StringComparison.OrdinalIgnoreCase) ||
                    !asset.Name.Contains("-gpl", StringComparison.OrdinalIgnoreCase) ||
                    asset.Name.Contains("-shared", StringComparison.OrdinalIgnoreCase) ||
                    !asset.Name.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
                    continue;

                assetUrl = asset.Browser_download_url;
                break;
            }
            if (assetUrl is null)
                throw new InvalidOperationException("FFmpeg not found on github");

            string ffmpegFileName = Path.GetFileName(assetUrl);
            string unzipedFolder = ffmpegFileName.Replace(".tar.xz", "", StringComparison.OrdinalIgnoreCase);

            // Download.
            Console.WriteLine($"Starting download of FFmpeg from {assetUrl}");
            using var client = new HttpClient();
            using var response = await client.GetAsync(assetUrl);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Some error during file download, status code {response.StatusCode}");

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(Path.Combine(CommonConsts.DefaultFFmpegFolder, ffmpegFileName), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await contentStream.CopyToAsync(fileStream);

            // Unzip file.
            Console.WriteLine($"Unzip {ffmpegFileName}");
            var command = Command.Run(
                "tar",
                "-xf", Path.Combine(CommonConsts.DefaultFFmpegFolder, ffmpegFileName),
                "-C", CommonConsts.DefaultFFmpegFolder);
            var result = await command.Task;
            if (!result.Success)
                throw new InvalidOperationException($"error in tar {result.StandardOutput} \n {result.StandardError}");

            // Copy file in default folder.
            if (ffmpeg)
                File.Copy($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}/bin/ffmpeg", $"{CommonConsts.DefaultFFmpegFolder}/ffmpeg", true);

            if (ffprobe)
                File.Copy($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}/bin/ffprobe", $"{CommonConsts.DefaultFFmpegFolder}/ffprobe", true);

            // Clean data.
            Directory.Delete($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}", true);
            File.Delete($"{CommonConsts.DefaultFFmpegFolder}/{ffmpegFileName}");

            Console.WriteLine($"FFmpeg {(ffprobe ? "and FFprobe " : "")}ready to use.");
            return CommonConsts.DefaultFFmpegFolder;
        }

        public static async Task<string> WindowsDownloadFFmpegAsync(bool ffmpeg, bool ffprobe, GitReleaseVersionDto gitReleaseVersionDto)
        {
            if (gitReleaseVersionDto is null)
                throw new ArgumentNullException(nameof(gitReleaseVersionDto));

            string? assetUrl = null;
            foreach (var asset in gitReleaseVersionDto.Assets)
            {
                if (!asset.Name.Contains("-win64", StringComparison.OrdinalIgnoreCase) ||
                    !asset.Name.Contains("-gpl", StringComparison.OrdinalIgnoreCase) ||
                    asset.Name.Contains("-shared", StringComparison.OrdinalIgnoreCase) ||
                    !asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                assetUrl = asset.Browser_download_url;
                break;
            }
            if (assetUrl is null)
                throw new InvalidOperationException("FFmpeg not found on github");

            string ffmpegFileName = Path.GetFileName(assetUrl);
            string unzipedFolder = Path.GetFileNameWithoutExtension(assetUrl);

            // Download.
            Console.WriteLine($"Starting download of FFmpeg from {assetUrl}");
            using var client = new HttpClient();
            using var response = await client.GetAsync(assetUrl);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Some error during file download, status code {response.StatusCode}");

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(Path.Combine(CommonConsts.DefaultFFmpegFolder, ffmpegFileName), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await contentStream.CopyToAsync(fileStream);
            await fileStream.DisposeAsync();
            await contentStream.DisposeAsync();

            // Unzip file.
            Console.WriteLine($"Unzip {ffmpegFileName}");
            if (Directory.Exists($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}"))
                Directory.Delete($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}", true);

            Directory.CreateDirectory($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}");
            ZipFile.ExtractToDirectory($"{CommonConsts.DefaultFFmpegFolder}/{ffmpegFileName}", $"{CommonConsts.DefaultFFmpegFolder}");

            // Copy file in default folder.
            if (ffmpeg)
                File.Copy($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}/bin/ffmpeg.exe", $"{CommonConsts.DefaultFFmpegFolder}/ffmpeg.exe", true);

            if (ffprobe)
                File.Copy($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}/bin/ffprobe.exe", $"{CommonConsts.DefaultFFmpegFolder}/ffprobe.exe", true);

            // Clean data.
            Directory.Delete($"{CommonConsts.DefaultFFmpegFolder}/{unzipedFolder}", true);
            File.Delete($"{CommonConsts.DefaultFFmpegFolder}/{ffmpegFileName}");

            Console.WriteLine($"FFmpeg {(ffprobe ? "and FFprobe " : "")}ready to use.");
            return CommonConsts.DefaultFFmpegFolder;
        }
    }

}
