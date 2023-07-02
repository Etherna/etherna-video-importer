using Medallion.Shell;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Utilities
{
    public static class FFmpegUtility
    {
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
    }
}
