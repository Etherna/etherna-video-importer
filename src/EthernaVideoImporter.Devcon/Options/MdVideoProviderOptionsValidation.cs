using Microsoft.Extensions.Options;
using System.IO;

namespace Etherna.VideoImporter.Devcon.Options
{
    internal sealed class MdVideoProviderOptionsValidation : IValidateOptions<MdVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, MdVideoProviderOptions options)
        {
            if (!Directory.Exists(options.MdSourceFolderPath))
                return ValidateOptionsResult.Fail($"Not found MD directory path at ({options.MdSourceFolderPath})");

            return ValidateOptionsResult.Success;
        }
    }
}
