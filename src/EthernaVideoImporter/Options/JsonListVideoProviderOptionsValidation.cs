using Etherna.VideoImporter.Core;
using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptionsValidation : IValidateOptions<JsonListVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonListVideoProviderOptions options)
        {
            if (options.JsonMetadataUri is null)
                return ValidateOptionsResult.Fail($"Json metadata uri can't be null");

            return ValidateOptionsResult.Success;
        }
    }
}
