using Etherna.ServicesClient.Clients.Index;
using System.Collections.Generic;

namespace Etherna.VideoImporter.Core.Dtos
{
    public sealed class MetadataManifestInsertInput
    {
        // Constructors.
        public MetadataManifestInsertInput(
            long createdAt,
            string ownerAddress,
            string? batchId,
            string? description,
            long? duration,
            string? originalQuality,
            string? personalData,
            MetadataImageInput? thumbnail,
            string? title,
            ICollection<SourceDto> sources)
        {
            CreatedAt = createdAt;
            OwnerAddress = ownerAddress;
            BatchId = batchId;
            Description = description;
            Duration = duration;
            Hash = "";
            OriginalQuality = originalQuality;
            PersonalData = personalData;
            Thumbnail = thumbnail;
            Title = title;
            Sources = sources ?? new List<SourceDto>();
        }

        // Properties.
        public long CreatedAt { get; }
        public string? BatchId { get; }
        public string? Description { get; }
        public long? Duration { get; }
        public string Hash { get; }
        public string? OriginalQuality { get; }
        public string OwnerAddress { get; }
        public string? PersonalData { get; }
        public long? UpdatedAt { get; }
        public string V { get; } = "1.1";
        public ICollection<SourceDto> Sources { get; }
        public MetadataImageInput? Thumbnail { get; }
        public string? Title { get; }
    }
}
