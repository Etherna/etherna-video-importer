using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace Etherna.VideoImporter.Core.Models.Cache
{
    public class CacheTracking
    {
        // Constructor.
        public CacheTracking(string videoIdHash)
        {
            Command = "TODO";
            VideoIdHash = videoIdHash;

            EncodedFiles = new();
            Thumbnails = new();
            VideoUploaded = new();
        }

        // Properties.
        public string VideoIdHash { get; set; }
        public string? BatchId { get; set; }
        public string Command { get; set; }
        public Dictionary<string, string> EncodedFiles { get; }
        public string? OriginalVideoFilePath { get; set; }
        public int OriginalVideoHeight { get; set; }
        public int OriginalVideoWidth { get; set; }
        public string? OriginalThumbnailFilePath { get; set; }
        public int OriginalThumbnailHeight { get; set; }
        public int OriginalThumbnailWidth { get; set; }
        public Dictionary<string, string> Thumbnails { get; }
        public string? VideoMetadataPath { get; set; }
        public Dictionary<string, string> VideoUploaded { get; }

        // Methods.
        public void AddEncodedFilePath(LocalFileBase localFileBase)
        {
            if (localFileBase is null)
                throw new ArgumentNullException(nameof(localFileBase));
            if (!File.Exists(localFileBase.FilePath))
                throw new FileNotFoundException(localFileBase.FilePath);

            string keyValue = localFileBase switch
            {
                AudioLocalFile _ => $"audio",
                VideoLocalFile evf => $"video_{evf.Height}_{evf.Width}",
                ThumbnailLocalFile etf => $"thumbnail_{etf.Height}_{etf.Width}",
                _ => throw new InvalidOperationException()
            };

            EncodedFiles[keyValue] = localFileBase.FilePath;
        }

        public void AddUploadedHash(LocalFileBase localFileBase, string batchId)
        {
            if (localFileBase is null)
                throw new ArgumentNullException(nameof(localFileBase));
            if (string.IsNullOrWhiteSpace(localFileBase.SwarmHash))
                throw new InvalidOperationException("SwarmHash empty");

            string keyValue = localFileBase switch
            {
                AudioLocalFile _ => $"audio_{batchId}",
                VideoLocalFile evf => $"video_{evf.Height}_{evf.Width}_{batchId}",
                ThumbnailLocalFile etf => $"thumbnail_{etf.Height}_{etf.Width}_{batchId}",
                _ => throw new InvalidOperationException()
            };

            EncodedFiles[keyValue] = localFileBase.SwarmHash;
        }

        public string? GetEncodedAudioFilePath() =>
            GetEncodedVideoFilePath(0, 0);

        public string? GetThumbnailFilePath(int height, int width)
        {
            Thumbnails.TryGetValue($"thumbnail_{height}_{width}", out var thumbnail);
            return thumbnail;
        }

        public string? GetEncodedVideoFilePath(int height, int width)
        {
            string keyValue;
            if (height > 0 ||
                width > 0)
                keyValue = $"video_{height}_{width}";
            else
                keyValue = $"audio";

            EncodedFiles.TryGetValue(keyValue, out var video);
            return video;
        }

        public string? GetUploadedHash(LocalFileBase localFileBase, string batchId)
        {
            string? swarmHash;
            _ = localFileBase switch
            {
                AudioLocalFile _ => EncodedFiles.TryGetValue($"audio_{batchId}", out swarmHash),
                VideoLocalFile evf => EncodedFiles.TryGetValue($"video_{evf.Height}_{evf.Width}_{batchId}", out swarmHash),
                ThumbnailLocalFile etf => EncodedFiles.TryGetValue($"thumbnail_{etf.Height}_{etf.Width}_{batchId}", out swarmHash),
                _ => throw new InvalidOperationException()
            };

            return swarmHash;
        }

        public void SaveOriginalVideo(VideoLocalFile videoLocalFile)
        {
            if (videoLocalFile is null)
                throw new ArgumentNullException(nameof(videoLocalFile));
            if (!File.Exists(videoLocalFile.FilePath))
                throw new FileNotFoundException(videoLocalFile.FilePath);

            OriginalVideoFilePath = videoLocalFile.FilePath;
            OriginalVideoHeight = videoLocalFile.Height;
            OriginalVideoWidth = videoLocalFile.Width;
        }

        public void SaveOriginalThumbnail(ThumbnailLocalFile thumbnailLocalFile)
        {
            if (thumbnailLocalFile is null)
                throw new ArgumentNullException(nameof(thumbnailLocalFile));
            if (!File.Exists(thumbnailLocalFile.FilePath))
                throw new FileNotFoundException(thumbnailLocalFile.FilePath);

            OriginalThumbnailFilePath = thumbnailLocalFile.FilePath;
            OriginalThumbnailHeight = thumbnailLocalFile.Height;
            OriginalThumbnailWidth = thumbnailLocalFile.Width;
        }
    }
}
