using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class FFmpegServiceOptionsValidation : IValidateOptions<FFmpegServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, FFmpegServiceOptions options)
        {
            if (!File.Exists(options.FFMpegBinaryPath))
                return ValidateOptionsResult.Fail($"FFmpeg not found at ({options.FFMpegBinaryPath})");
            if (!File.Exists(options.FFProbeBinaryPath))
                return ValidateOptionsResult.Fail($"FFprobe not found at ({options.FFProbeBinaryPath})");

            return ValidateOptionsResult.Success;
        }
    }
}
