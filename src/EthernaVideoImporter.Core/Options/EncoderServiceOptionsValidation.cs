using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class EncoderServiceOptionsValidation : IValidateOptions<EncoderServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, EncoderServiceOptions options)
        {
            if (!File.Exists(options.FFMpegBinaryPath))
                return ValidateOptionsResult.Fail($"FFmpeg not found at ({options.FFMpegBinaryPath})");

            return ValidateOptionsResult.Success;
        }
    }
}
