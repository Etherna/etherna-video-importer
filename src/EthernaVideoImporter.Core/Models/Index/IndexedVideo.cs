using Etherna.VideoImporter.Core.Models.Domain;
using System;

namespace Etherna.VideoImporter.Core.Models.Index
{
    public sealed class IndexedVideo
    {
        // Constructors.
        public IndexedVideo(ServicesClient.Clients.Index.VideoDto video)
        {
            if (video is null)
                throw new ArgumentNullException(nameof(video));

            IndexId = video.Id;
            CreationDateTime = video.CreationDateTime;
            if (video.LastValidManifest is not null)
                LastValidManifest = new IndexedVideoManifest(video.LastValidManifest);
        }

        // Properties.
        public string IndexId { get; set; }
        public DateTimeOffset CreationDateTime { get; set; }
        public IndexedVideoManifest? LastValidManifest { get; set; }

        // Internal methods.
        internal bool IsEqualTo(VideoMetadataBase sourceMetadata) =>
            LastValidManifest?.PersonalData?.VideoId == sourceMetadata.Id &&
            LastValidManifest?.Title == sourceMetadata.Title &&
            LastValidManifest?.Description == sourceMetadata.Description;
    }
}
