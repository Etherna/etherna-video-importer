using Etherna.VideoImporter.Core.Models.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEncoderService
    {
        Task<ThumbnailSourceFile> CreateRandomThumbnailAsync(VideoSourceFile videoSourceFile);
        Task<IEnumerable<VideoSourceFile>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile);
    }
}
