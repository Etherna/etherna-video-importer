using Microsoft.Extensions.Options;

namespace Etherna.VideoImporter.Core.Options
{
    internal sealed class VideoUploaderServiceOptionsValidation : IValidateOptions<VideoUploaderServiceOptions>
    {
        public ValidateOptionsResult Validate(string? name, VideoUploaderServiceOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.UserEthAddr))
                return ValidateOptionsResult.Fail("Missing user's ethereum address");

            return ValidateOptionsResult.Success;
        }
    }
}
