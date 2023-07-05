using Etherna.VideoImporter.Core.Models.Domain;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptions
    {
        public SourceUri JsonMetadataUri { get; set; } = default!;
    }
}
