using Etherna.VideoImporter.Core.Models.Cache;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class CacheService : ICacheService
    {
        // Consts.
        private const string MetadataFileName = "metadata.json";
        private const string TrackingFileName = "tracking.json";

        // Fields.
        private readonly CacheServiceOptions options;

        // Constructor.
        public CacheService(IOptions<CacheServiceOptions> options)
        {
            this.options = options.Value;
        }

        // Properties.
        public bool IsActive => options.CacheEnable;

        // Methods.
        public async Task AddVideoMetadataAsync<T>(
            T videoMetadata,
            CacheTracking? cacheTracking) where T : VideoMetadataBase
        {
            if (!IsActive)
                return;

            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));

            var workerVideoInfo = CreateAndGetWorkingDirectory(cacheTracking.VideoIdHash);
            var metadataFilePath = GetMetadataFilePath(workerVideoInfo);

            await File.WriteAllTextAsync(metadataFilePath, JsonSerializer.Serialize(videoMetadata));
            cacheTracking.VideoMetadataPath = metadataFilePath;
            await SaveTrackingAsync(cacheTracking);
        }

        public string GetCachePathDirectory(string videoIdHash)
        {
            if (string.IsNullOrWhiteSpace(videoIdHash))
                throw new ArgumentNullException(nameof(videoIdHash));

            return Path.Combine(options.CacheFolderPath, videoIdHash);
        }

        public string GetCachePathDirectory(CacheTracking cacheTracking)
        {
            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));

            return GetCachePathDirectory(cacheTracking.VideoIdHash);
        }

        public async Task<CacheTracking?> GetTrackingAsync(string videoIdHash)
        {
            if (!IsActive)
                return null;

            if (string.IsNullOrWhiteSpace(videoIdHash))
                throw new ArgumentNullException(nameof(videoIdHash));

            var workerVideoInfo = CreateAndGetWorkingDirectory(videoIdHash);
            var trackingFilePath = GetTrackingFilePath(workerVideoInfo);

            if (!File.Exists(trackingFilePath))
                return new CacheTracking(videoIdHash);

            return JsonSerializer.Deserialize<CacheTracking>(await File.ReadAllTextAsync(trackingFilePath)) ??
                new CacheTracking(videoIdHash);
        }

        public async Task<T?> GetVideoMetadataAsync<T>(CacheTracking? cacheTracking) where T : VideoMetadataBase
        {
            if (!IsActive)
                return null;

            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));

            var workerVideoInfo = CreateAndGetWorkingDirectory(cacheTracking.VideoIdHash);
            var metadataFilePath = GetMetadataFilePath(workerVideoInfo);

            if (!File.Exists(metadataFilePath))
                return null;

            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(metadataFilePath));
        }

        public async Task SaveTrackingAsync(CacheTracking? trackingStatus)
        {
            if (!IsActive)
                return;

            if (trackingStatus is null)
                throw new ArgumentNullException(nameof(trackingStatus));

            var workerVideoInfo = CreateAndGetWorkingDirectory(trackingStatus.VideoIdHash);
            var trackingFilePath = GetTrackingFilePath(workerVideoInfo);

            await File.WriteAllTextAsync(trackingFilePath, JsonSerializer.Serialize(trackingStatus));
            trackingStatus.VideoMetadataPath = trackingFilePath;
        }

        // Helpers.
        private DirectoryInfo CreateAndGetWorkingDirectory(string videoIdHash)
        {
            var workingDirectory = GetCachePathDirectory(videoIdHash);
            return Directory.CreateDirectory(workingDirectory);
        }

        private string GetMetadataFilePath(DirectoryInfo workerVideoInfo) =>
            $"{workerVideoInfo.FullName}/{MetadataFileName}";

        private string GetTrackingFilePath(DirectoryInfo workerVideoInfo) =>
            $"{workerVideoInfo.FullName}/{TrackingFileName}";
    }
}
