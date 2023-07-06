using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class EncoderServiceOptionsValidation : IValidateOptions<EncoderServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, EncoderServiceOptions options)
        {
            if (!File.Exists(options.FFMpegBinaryPath))
                return ValidateOptionsResult.Fail($"FFmpeg not found at ({options.FFMpegBinaryPath})");

            if (!EncoderServiceOptions.PresetCodecs.Any(pc => pc == options.PresetCodec))
                return ValidateOptionsResult.Fail($"{options.PresetCodec} it'sn an allowed value.");
            
            return ValidateOptionsResult.Success;
        }
    }
}
