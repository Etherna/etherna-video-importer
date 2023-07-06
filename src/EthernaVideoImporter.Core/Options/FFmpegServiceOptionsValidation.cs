using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class FFmpegServiceOptionsValidation : IValidateOptions<FFmpegServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, FFmpegServiceOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FFmpegFolderPath))
                return ValidateOptionsResult.Fail($"FFmpeg folder path can't be null or white spaces");

            return ValidateOptionsResult.Success;
        }
    }
}
