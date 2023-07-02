using Etherna.VideoImporter.Services;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptionsValidation : IValidateOptions<JsonListVideoProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, JsonListVideoProviderOptions options)
        {
            if (!File.Exists(options.FFProbeBinaryPath))
                return ValidateOptionsResult.Fail($"FFProbe not found at ({options.FFProbeBinaryPath})");
            if (string.IsNullOrWhiteSpace(options.JsonMetadataUri))
                return ValidateOptionsResult.Fail($"Json metadata path can't be null or white spaces");

            switch (options.JsonMetadataUriType)
            {
                case Core.Models.Domain.UriType.Absolute:
                case Core.Models.Domain.UriType.Relative:
                    if (!File.Exists(options.JsonMetadataAbsoluteUri))
                        return ValidateOptionsResult.Fail($"JSON metadata video list not found at \"{options.JsonMetadataAbsoluteUri}\"");
                    break;

                case Core.Models.Domain.UriType.Url:
                    var getJsonDataTask = JsonListVideoProvider.TryGetJsonDataOnlineAsync(options.JsonMetadataAbsoluteUri);
                    getJsonDataTask.Wait();
                    var jsonData = getJsonDataTask.Result;

                    if (jsonData is null)
                        return ValidateOptionsResult.Fail($"JSON metadata video list not found at \"{options.JsonMetadataAbsoluteUri}\"");
                    break;

                default: throw new InvalidOperationException();
            }

            return ValidateOptionsResult.Success;
        }
    }
}
