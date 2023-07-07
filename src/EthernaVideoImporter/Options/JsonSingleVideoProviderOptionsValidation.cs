using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonSingleVideoProviderOptionsValidation : IValidateOptions<JsonSingleVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonSingleVideoProviderOptions options)
        {
            if (!File.Exists(options.FFProbeBinaryPath))
                return ValidateOptionsResult.Fail($"FFProbe not found at ({options.FFProbeBinaryPath})");
            if (string.IsNullOrWhiteSpace(options.Title))
                return ValidateOptionsResult.Fail("Title is mandatory");
            if (string.IsNullOrWhiteSpace(options.Description))
                return ValidateOptionsResult.Fail("Description is mandatory");
            if (string.IsNullOrWhiteSpace(options.VideoUri))
                return ValidateOptionsResult.Fail("Video uri is mandatory");

            return ValidateOptionsResult.Success;
        }
    }
}
