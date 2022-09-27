using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;

namespace EthernaVideoImporter.Models
{
    internal class VideoInfoWithData
    {
        // Video Infos.
        
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? Expertise { get; set; }
        public string? IpfsHash { get; set; }
        public IEnumerable<string>? Keywords { get; set; }
        public IEnumerable<string>? Tags { get; set; }
        public string? Title { get; set; }
        public string? YoutubeUrl { get; set; }

        // Video Data.

        [Optional]
        public int Bitrate { get; set; }
        [Optional]
        public string? BatchId { get; set; }
        [Optional]
        public string? BatchReferenceId { get; set; }
        public string? Description { get; set; }
        [Optional]
        public string? DownloadedFileName { get; set; }
        [Optional]
        public string? DownloadedFilePath { get; set; }
        [Optional]
        public string? DownloadedThumbnailPath { get; set; }
        [Optional]
        public string? IndexVideoId { get; set; }
        [Optional]
        public string? HashMetadataReference { get; set; }
        [Optional]
        public string? Quality { get; set; }
        [Optional]
        public long Size { get; set; }
        [Optional]
        public string? ThumbnailReference { get; set; }
        [Optional]
        public string? VideoReference { get; set; }
        [Optional]
        public VideoStatus? VideoStatus { get; set; }
        [Optional]
        public string? VideoStatusNote { get; set; }
        [Optional]
        public string? EmbedLink { get; set; }
        
    }
}
