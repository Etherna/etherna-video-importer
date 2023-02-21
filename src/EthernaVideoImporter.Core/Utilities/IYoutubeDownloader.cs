using Etherna.VideoImporter.Core.Models;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Utilities
{
    public interface IYoutubeDownloader
    {
        Task<Video> GetVideoAsync(bool includeAudioTrack, YouTubeVideoMetadataBase videoMetadata);
    }
}