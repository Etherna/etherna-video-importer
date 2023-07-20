using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class FFmpegServiceOptionsValidation : IValidateOptions<FFmpegServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, FFmpegServiceOptions options)
        {
            if (options.FFmpegFolderPath is null)
                return ValidateOptionsResult.Fail($"FFmpeg folder path can't be null");

            return ValidateOptionsResult.Success;
        }
    }
}
