using System;
using System.Collections.Generic;
using System.Text.Json;
using Etherna.VideoImporter.Core.Dtos;

namespace Etherna.VideoImporter.Core.Models
{
    public sealed class IndexedVideoManifest
    {
        // Constructors.
        public IndexedVideoManifest(ServicesClient.Clients.Index.VideoManifestDto lastValidManifest)
        {
            if (lastValidManifest is null)
                throw new ArgumentNullException(nameof(lastValidManifest));

            BatchId = lastValidManifest.BatchId ?? "";
            Description = lastValidManifest.Description ?? "";
            Duration = lastValidManifest.Duration ?? 0;
            Hash = lastValidManifest.Hash;
            OriginalQuality = lastValidManifest.OriginalQuality ?? "";
            PersonalData = JsonSerializer.Deserialize<ManifestPersonalDataDto>(lastValidManifest.PersonalData!);
            RawPersonalData = lastValidManifest.PersonalData;
            Sources = lastValidManifest.Sources;
            Thumbnail = lastValidManifest.Thumbnail;
            Title = lastValidManifest.Title ?? "";
        }

        // Properties.
        public string BatchId { get; set; }
        public string Description { get; set; }
        public long Duration { get; set; }
        public string Hash { get; set; }
        public string OriginalQuality { get; set; }
        public ManifestPersonalDataDto? PersonalData { get; set; }
        public string? RawPersonalData { get; set; }
        public IEnumerable<ServicesClient.Clients.Index.SourceDto> Sources { get; set; }
        public ServicesClient.Clients.Index.ImageDto Thumbnail { get; set; }
        public string Title { get; set; }
    }
}
