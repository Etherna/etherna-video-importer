using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Options
{
    internal sealed class YouTubeChannelVideoProviderOptionsValidation : IValidateOptions<YouTubeChannelVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, YouTubeChannelVideoProviderOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ChannelUrl))
                return ValidateOptionsResult.Fail("Invalid YouTube channel url");

            return ValidateOptionsResult.Success;
        }
    }
}
