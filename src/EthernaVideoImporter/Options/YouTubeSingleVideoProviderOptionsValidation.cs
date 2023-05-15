using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Options
{
    internal sealed class YouTubeSingleVideoProviderOptionsValidation : IValidateOptions<YouTubeSingleVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, YouTubeSingleVideoProviderOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.VideoUrl))
                return ValidateOptionsResult.Fail("Invalid YouTube video url");

            return ValidateOptionsResult.Success;
        }
    }
}
