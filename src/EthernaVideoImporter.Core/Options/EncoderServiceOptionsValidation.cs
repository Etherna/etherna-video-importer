using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class EncoderServiceOptionsValidation : IValidateOptions<EncoderServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, EncoderServiceOptions options)
        {
            if (options.FFMpegBinaryPath is null)
                return ValidateOptionsResult.Fail($"FFmpeg not found");

            return ValidateOptionsResult.Success;
        }
    }
}
