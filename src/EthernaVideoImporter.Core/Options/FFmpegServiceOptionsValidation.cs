using Microsoft.Extensions.Options;
using System.Linq;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class FFmpegServiceOptionsValidation : IValidateOptions<FFmpegServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, FFmpegServiceOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FFmpegFolderPath))
                return ValidateOptionsResult.Fail($"FFmpeg folder path can't be null or white spaces");
            if (!FFmpegServiceOptions.PresetCodecs.Any(pc => pc == options.PresetCodec))
                return ValidateOptionsResult.Fail($"{options.PresetCodec} it is not an allowed value.");

            return ValidateOptionsResult.Success;
        }
    }
}
