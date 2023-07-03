using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class EncoderServiceOptionsValidation : IValidateOptions<EncoderServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, EncoderServiceOptions options)
        {
            if (options.FFMpegBinaryPath is null ||
                (options.FFMpegBinaryPath != CommonConsts.FFMpegBinaryName && 
                 !File.Exists(options.FFMpegBinaryPath)))
                return ValidateOptionsResult.Fail($"FFmpeg not found");

            return ValidateOptionsResult.Success;
        }
    }
}
