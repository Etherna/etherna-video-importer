using Medallion.Shell;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Utilities
{
    public static class FFmpegUtility
    {
        // Methods.
        public static async Task<string?> FFmpegCheckAndGetAsync(string? customFFMpegFolderPath)
        {
            return await CheckAndGetAsync(customFFMpegFolderPath, CommonConsts.FFmpegBinaryName, CommonConsts.DefaultFFmpegFolder);
        }

        public static async Task<string?> FFProbeCheckAndGetAsync(string? customFFMpegFolderPath)
        {
            return await CheckAndGetAsync(customFFMpegFolderPath, CommonConsts.FFprobeBinaryName, CommonConsts.DefaultFFmpegFolder);
        }

        // Helpers.
        public static async Task<string?> CheckAndGetAsync(string? customFFmpegFolderPath, string binaryName, string defaultFFmpegFolderPath)
        {
            // Custom FFmpeg folder.
            if (!string.IsNullOrWhiteSpace(customFFmpegFolderPath))
            {
                if (!Directory.Exists(customFFmpegFolderPath))
                    return null; // Not found.

                try
                {
                    var command = Command.Run($"{customFFmpegFolderPath}/{binaryName}", "-version");
                    var result = await command.Task;
                    if (result.Success)
                        return customFFmpegFolderPath;
                }
                catch (System.ComponentModel.Win32Exception) { }
            }

            // Default FFmpeg folder.
            try
            {
                var command = Command.Run($"{defaultFFmpegFolderPath}/{binaryName}", "-version");
                var result = await command.Task;
                if (result.Success)
                    return defaultFFmpegFolderPath;
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Global FFmpeg.
            try
            {
                var command = Command.Run(binaryName, "-version");
                var result = await command.Task;
                if (result.Success)
                    return "";
            }
            catch (System.ComponentModel.Win32Exception) { }

            // Not found.
            return null;
        }
    }

}
