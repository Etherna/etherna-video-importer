using Etherna.VideoImporter.Core.Models.Cache;
using Etherna.VideoImporter.Core.Models.Domain;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface ICacheService
    {
        // Properties.
        bool IsActive { get; }

        // Methods.
        Task AddVideoMetadataAsync<T>(T videoMetadata, CacheTracking? trackingStatus) where T : VideoMetadataBase;
        string? GetCachePathDirectory(string videoIdHash);
        string? GetCachePathDirectory(CacheTracking trackingStatus);
        Task<CacheTracking?> GetTrackingAsync(string videoIdHash);
        Task<T?> GetVideoMetadataAsync<T>(CacheTracking? trackingStatus) where T : VideoMetadataBase;
        Task SaveTrackingAsync(CacheTracking? trackingStatus);
    }
}
