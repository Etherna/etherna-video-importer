using Etherna.VideoImporter.Core.Models.Domain;

namespace Etherna.VideoImporter.Options
{
    internal sealed class JsonListVideoProviderOptions
    {
        public bool GenerateThumbnailWhenMissing { get; set; }
        public SourceUri JsonMetadataUri { get; set; } = default!;
    }
}
