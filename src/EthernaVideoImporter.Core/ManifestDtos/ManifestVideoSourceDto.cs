using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
using System;

namespace Etherna.VideoImporter.Core.ManifestDtos
{
    internal sealed class ManifestVideoSourceDto
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
            Bitrate = videoFile.Bitrate;
            Quality = videoFile.Resolution;
            Reference = videoFile.UploadedVideoReference ?? throw new InvalidOperationException();
            Size = videoFile.Size;
        }

        public int? Bitrate { get; }
        public string Quality { get; }
        public string Reference { get; }
        public long Size { get; }
    }
}
