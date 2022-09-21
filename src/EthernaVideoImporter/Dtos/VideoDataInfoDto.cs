using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;

namespace EthernaVideoImporter.Dtos
{
    internal class VideoDataInfoDto
    {
        [Optional]
        public int Bitrate { get; set; }
        [Optional]
        public string? BatchId { get; set; }
        public string? Description { get; set; }
        [Optional]
        public string? DownloadedFileName { get; set; }
        [Optional]
        public string? DownloadedFilePath { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? Expertise { get; set; }
        [Optional]
        public string? IndexVideoId { get; set; }
        public string? IpfsHash { get; set; }
        [Optional]
        public string? MetadataReference { get; set; }
        public IEnumerable<string>? Keywords { get; set; }
        [Optional]
        public string? Quality { get; set; }
        [Optional]
        public long Size { get; set; }
        [Optional]
        public string? VideoReference { get; set; }
        [Optional]
        public VideoStatus? VideoStatus { get; set; }
        [Optional]
        public string? VideoStatusNote { get; set; }
        public IEnumerable<string>? Tags { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Track { get; set; }
        public string? YoutubeUrl { get; set; }
    }
}
