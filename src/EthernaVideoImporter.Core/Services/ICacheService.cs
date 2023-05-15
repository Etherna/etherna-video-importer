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
        Task AddVideoMetadataAsync<T>(T videoMetadata, CacheTracking? trackingStatus, DirectoryInfo tempDirectoryInfo) where T : VideoMetadataBase;
        string? GetCachePathDirectory(string videoIdHash, DirectoryInfo tempDirectoryInfo);
        string? GetCachePathDirectory(CacheTracking trackingStatus, DirectoryInfo tempDirectoryInfo);
        Task<CacheTracking?> GetTrackingAsync(string videoIdHash, DirectoryInfo tempDirectoryInfo);
        Task<T?> GetVideoMetadataAsync<T>(CacheTracking? trackingStatus, DirectoryInfo tempDirectoryInfo) where T : VideoMetadataBase;
        Task SaveTrackingAsync(CacheTracking? trackingStatus, DirectoryInfo tempDirectoryInfo);
    }
}
