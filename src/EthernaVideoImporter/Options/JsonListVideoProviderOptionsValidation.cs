using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptionsValidation : IValidateOptions<JsonListVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonListVideoProviderOptions options)
        {
            if (!File.Exists(options.FFProbeBinaryPath))
                return ValidateOptionsResult.Fail($"FFProbe not found at ({options.FFProbeBinaryPath})");
            if (options.JsonMetadataUri is null)
                return ValidateOptionsResult.Fail($"Json metadata uri can't be null");

            var existsTask = options.JsonMetadataUri.ExistsAsync(true);
            existsTask.Wait();
            if (!existsTask.Result)
                return ValidateOptionsResult.Fail($"JSON metadata video list not found at \"{options.JsonMetadataUri.OriginalUri}\"");

            return ValidateOptionsResult.Success;
        }
    }
}
