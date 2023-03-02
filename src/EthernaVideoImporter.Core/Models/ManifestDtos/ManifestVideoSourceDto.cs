using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using System;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestVideoSourceDto
    {
        public ManifestVideoSourceDto(SourceDto source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            Quality = source.Quality;
            Reference = source.Reference;
            Size = source.Size;
        }

        public ManifestVideoSourceDto(VideoFile videoFile)
        {
            if (videoFile is null)
                throw new ArgumentNullException(nameof(videoFile));

            Quality = videoFile.VideoQualityLabel;
            Reference = videoFile.UploadedHashReference ?? throw new InvalidOperationException();
            Size = videoFile.ByteSize;
        }

        public int Bitrate => 420; //fake place holder, will be removed on manifest 2.0
        public string Quality { get; }
        public string Reference { get; }
        public long Size { get; }
    }
}
