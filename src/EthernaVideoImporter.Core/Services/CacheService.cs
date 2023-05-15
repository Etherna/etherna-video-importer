using Etherna.VideoImporter.Core.Models.Cache;
using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    internal sealed class CacheService
    {
        // Consts.
        private const string MetadataFileName = "metadata.json";
        private const string TrackingFileName = "tracking.json";

        // Fields.
        private readonly bool cacheEnable;

        // Constructor.
        public CacheService(bool cacheEnable)
        {
            this.cacheEnable = cacheEnable;
        }

        // Properties.
        public bool IsActive => cacheEnable;

        // Methods.
        public async Task AddVideoMetadataAsync<T>(
            T videoMetadata,
            CacheTracking? cacheTracking,
            DirectoryInfo tempDirectoryInfo) where T : VideoMetadataBase
        {
            if (!IsActive)
                return;

            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            var workerVideoInfo = CreateAndGetWorkingDirectory(cacheTracking.VideoIdHash, tempDirectoryInfo);
            var metadataFilePath = GetMetadataFilePath(workerVideoInfo);

            await File.WriteAllTextAsync(metadataFilePath, JsonSerializer.Serialize(videoMetadata));
            cacheTracking.VideoMetadataPath = metadataFilePath;
            await SaveTrackingAsync(cacheTracking, tempDirectoryInfo);
        }

        public string GetCachePathDirectory(string videoIdHash, DirectoryInfo tempDirectoryInfo)
        {
            if (string.IsNullOrWhiteSpace(videoIdHash))
                throw new ArgumentNullException(nameof(videoIdHash));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            return Path.Combine(tempDirectoryInfo.FullName, videoIdHash);
        }

        public string GetCachePathDirectory(CacheTracking cacheTracking, DirectoryInfo tempDirectoryInfo)
        {
            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            return GetCachePathDirectory(cacheTracking.VideoIdHash, tempDirectoryInfo);
        }

        public async Task<CacheTracking?> GetTrackingAsync(
            string videoIdHash,
            DirectoryInfo tempDirectoryInfo)
        {
            if (!IsActive)
                return null;

            if (string.IsNullOrWhiteSpace(videoIdHash))
                throw new ArgumentNullException(nameof(videoIdHash));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            var workerVideoInfo = CreateAndGetWorkingDirectory(videoIdHash, tempDirectoryInfo);
            var trackingFilePath = GetTrackingFilePath(workerVideoInfo);

            if (!File.Exists(trackingFilePath))
                return new CacheTracking(videoIdHash);

            return JsonSerializer.Deserialize<CacheTracking>(await File.ReadAllTextAsync(trackingFilePath)) ??
                new CacheTracking(videoIdHash);
        }

        public async Task<T?> GetVideoMetadataAsync<T>(
            CacheTracking? cacheTracking,
            DirectoryInfo tempDirectoryInfo) where T : VideoMetadataBase
        {
            if (!IsActive)
                return null;

            if (cacheTracking is null)
                throw new ArgumentNullException(nameof(cacheTracking));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            var workerVideoInfo = CreateAndGetWorkingDirectory(cacheTracking.VideoIdHash, tempDirectoryInfo);
            var metadataFilePath = GetMetadataFilePath(workerVideoInfo);

            if (!File.Exists(metadataFilePath))
                return null;

            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(metadataFilePath));
        }

        public async Task SaveTrackingAsync(CacheTracking? trackingStatus, DirectoryInfo tempDirectoryInfo)
        {
            if (!IsActive)
                return;

            if (trackingStatus is null)
                throw new ArgumentNullException(nameof(trackingStatus));
            if (tempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(tempDirectoryInfo));

            var workerVideoInfo = CreateAndGetWorkingDirectory(trackingStatus.VideoIdHash, tempDirectoryInfo);
            var trackingFilePath = GetTrackingFilePath(workerVideoInfo);

            await File.WriteAllTextAsync(trackingFilePath, JsonSerializer.Serialize(trackingStatus));
            trackingStatus.VideoMetadataPath = trackingFilePath;
        }

        // Helpers.
        private DirectoryInfo CreateAndGetWorkingDirectory(
            string videoIdHash,
            DirectoryInfo tempDirectoryInfo)
        {
            var workingDirectory = GetCachePathDirectory(videoIdHash, tempDirectoryInfo);
            return Directory.CreateDirectory(workingDirectory);
        }

        private string GetMetadataFilePath(DirectoryInfo workerVideoInfo) =>
            $"{workerVideoInfo.FullName}/{MetadataFileName}";

        private string GetTrackingFilePath(DirectoryInfo workerVideoInfo) =>
            $"{workerVideoInfo.FullName}/{TrackingFileName}";
    }
}
