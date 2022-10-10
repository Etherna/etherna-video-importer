using CsvHelper.Configuration.Attributes;
using Etherna.EthernaVideoImporter.Models;
using System.Collections.Generic;

namespace EthernaVideoImporter.Models
{
    internal class VideoInfoWithData
    {
        // Video Infos.
        public string? Description { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? Title { get; set; }
        public string? IpfsHash { get; set; }
        public string? Type { get; set; }
        public string? YoutubeUrl { get; set; }

        // Video Data.

        [Optional]
        public int Bitrate { get; set; }
        [Optional]
        public string? BatchId { get; set; }
        [Optional]
        public string? BatchReferenceId { get; set; }
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
        public ImportStatus? ImportStatus { get; set; }
        [Optional]
        public CsvItemStatus? CsvItemStatus { get; set; }
        [Optional]
        public string? VideoStatusNote { get; set; }
        [Optional]
        public string? EmbedDecentralizedLink { get; set; }
        [Optional]
        public string? EmbedIndexLink { get; set; }

    }
}
