using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
using System;

namespace Etherna.VideoImporter.Core.Dtos
{
    public sealed class ManifestVideoSourceDto
    {
        public ManifestVideoSourceDto(SourceDto source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            Bitrate = source.Bitrate;
            Quality = source.Quality;
            Reference = source.Reference;
            Size = source.Size;
        }

        public ManifestVideoSourceDto(EncodedVideoFile videoFile)
        {
            if (videoFile is null)
                throw new ArgumentNullException(nameof(videoFile));

            Bitrate = videoFile.Bitrate;
            Quality = videoFile.VideoQualityLabel;
            Reference = videoFile.UploadedHashReference ?? throw new InvalidOperationException();
            Size = videoFile.ByteSize;
        }

        public int? Bitrate { get; }
        public string Quality { get; }
        public string Reference { get; }
        public long Size { get; }
    }
}
