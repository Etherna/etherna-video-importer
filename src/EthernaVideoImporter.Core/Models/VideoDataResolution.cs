using System;

namespace Etherna.VideoImporter.Core.Models
{
    public class VideoDataResolution
    {
        // Constructors.
        public VideoDataResolution(
            long audioBitrate,
            string downloadedFilePath,
            string name,
            string resolution)
        {
            AudioBitrate = audioBitrate;
            DownloadedFilePath = downloadedFilePath;
            Name = name;
            Resolution = resolution;
        }

        // Properties.
        public long AudioBitrate { get; protected set; }
        public int Bitrate { get; protected set; }
        public int Duration { get; protected set; } // Seconds.
        public string? DownloadedFileName { get; protected set; }
        public string DownloadedFilePath { get; protected set; }
        public string? DownloadedThumbnailPath { get; protected set; }
        public string? UploadedThumbnailReference { get; protected set; }
        public string? UploadedVideoReference { get; protected set; }
        public string Name { get; protected set; }
        public string Resolution { get; protected set; }
        public long Size { get; protected set; } // Bytes.

        // Methods.
        public void SetVideoInfo(
            string filename,
            long fileSize,
            int duration)
        {
            DownloadedFileName = filename;
            Size = fileSize;
            Duration = duration;
            Bitrate = (int)Math.Ceiling((double)fileSize * 8 / duration);
        }

        public void SetDownloadThumbnail(string? downloadedThumbnailPath)
        {
            DownloadedThumbnailPath = downloadedThumbnailPath;
        }

        public void SetUploadedThumbnailReference(string uploadedThumbnailReference)
        {
            UploadedThumbnailReference = uploadedThumbnailReference;
        }

        public void SetUploadedVideoReference(string uploadedVideoReference)
        {
            UploadedVideoReference = uploadedVideoReference;
        }
    }
}
