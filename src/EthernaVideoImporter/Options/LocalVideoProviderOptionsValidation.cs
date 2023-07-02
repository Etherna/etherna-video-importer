using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class LocalVideoProviderOptionsValidation : IValidateOptions<LocalVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, LocalVideoProviderOptions options)
        {
            if (options.FFProbeBinaryPath is null ||
                (!string.IsNullOrWhiteSpace(options.FFProbeBinaryPath) &&
                 !File.Exists(options.FFProbeBinaryPath)))
                return ValidateOptionsResult.Fail($"FFProbe not found at ({options.FFProbeBinaryPath})");
            if (!File.Exists(options.JsonMetadataFilePath))
                return ValidateOptionsResult.Fail($"Local videos JSON metadata not found at ({options.JsonMetadataFilePath})");

            return ValidateOptionsResult.Success;
        }
    }
}
