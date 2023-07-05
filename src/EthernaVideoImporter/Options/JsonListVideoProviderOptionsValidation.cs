using Etherna.VideoImporter.Core;
using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptionsValidation : IValidateOptions<JsonListVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonListVideoProviderOptions options)
        {
            if (options.FFProbeBinaryPath is null ||
                (options.FFProbeBinaryPath != CommonConsts.FFProbeBinaryName &&
                 !File.Exists(options.FFProbeBinaryPath)))
                return ValidateOptionsResult.Fail($"FFProbe not found at ({options.FFProbeBinaryPath})");
            if (!File.Exists(options.JsonMetadataFilePath))
                return ValidateOptionsResult.Fail($"Local videos JSON metadata not found at ({options.JsonMetadataFilePath})");

            return ValidateOptionsResult.Success;
        }
    }
}
