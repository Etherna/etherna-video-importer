using Etherna.VideoImporter.Core.Models.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEncoderService
    {
        Task<IEnumerable<VideoSourceFile>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile);
    }
}
