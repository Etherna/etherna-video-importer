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
            return await CheckAndGetAsync(customFFMpegFolderPath, CommonConsts.FFMpegBinaryName, CommonConsts.DefaultFFmpegFolder);
        }

        public static async Task<string?> FFProbeCheckAndGetAsync(string? customFFMpegFolderPath)
        {
            return await CheckAndGetAsync(customFFMpegFolderPath, CommonConsts.FFProbeBinaryName, CommonConsts.DefaultFFmpegFolder);
        }

        // Helpers.
        public static async Task<string?> CheckAndGetAsync(string? customFFMpegFolderPath, string binaryName, string defaultFFMpegFolderPath)
        {
            // Custom FFmpeg folder.
            if (!string.IsNullOrWhiteSpace(customFFMpegFolderPath))
            {
                if (!Directory.Exists(customFFMpegFolderPath))
                    return null; // Not found.

                try
                {
                    var command = Command.Run($"{customFFMpegFolderPath}/{binaryName}", "-version");
                    var result = await command.Task;
                    if (result.Success)
                        return customFFMpegFolderPath;
                }
                catch (System.ComponentModel.Win32Exception) { }
            }

            // Default FFmpeg folder.
            try
            {
                var command = Command.Run($"{defaultFFMpegFolderPath}/{binaryName}", "-version");
                var result = await command.Task;
                if (result.Success)
                    return defaultFFMpegFolderPath;
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
