using System.Collections.Generic;
using System.Text.Json;

namespace Etherna.EthernaVideoImporter.Dtos
{
    internal class MetadataVideoDto
    {
        // Constructors.
        public MetadataVideoDto(
            string? batchId,
            string description,
            long duration,
            long createdAt,
            string originalQuality,
            string ownerAddress,
            IEnumerable<MetadataVideoSource> sources,
            SwarmImageRaw? thumbnail,
            string title,
            long? updatedAt)
        {
            BatchId = batchId;
            Description = description;
            Duration = duration;
            CreatedAt = createdAt;
            OriginalQuality = originalQuality;
            OwnerAddress = ownerAddress;
            Sources = sources;
            Thumbnail = thumbnail;
            Title = title;
            UpdatedAt = updatedAt;
        }

        // Properties.
        public string? BatchId { get; }
        public string Description { get; }
        public long Duration { get; }
        public long CreatedAt { get; }
        public string OriginalQuality { get; }
        public string OwnerAddress { get; }
        public IEnumerable<MetadataVideoSource> Sources { get; }
        public SwarmImageRaw? Thumbnail { get; }
        public string Title { get; }
        public long? UpdatedAt { get; }

        //  Public Methods.
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
