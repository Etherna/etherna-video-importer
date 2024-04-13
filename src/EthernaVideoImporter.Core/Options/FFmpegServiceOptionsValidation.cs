using Microsoft.Extensions.Options;
using System.Linq;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class FFmpegServiceOptionsValidation : IValidateOptions<FFmpegServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, FFmpegServiceOptions options)
        {
            if (!FFmpegServiceOptions.PresetCodecs.Any(pc => pc == options.PresetCodec))
                return ValidateOptionsResult.Fail($"{options.PresetCodec} It is not an allowed value");

            return ValidateOptionsResult.Success;
        }
    }
}
