using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;

namespace EthernaVideoImporter.Dtos
{
    internal class VideoDataInfoDto
    {
        public string? Description { get; set; }
        [Optional]
        public string? DownloadedFileName { get; set; }
        public string? DownloadedFilePath { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? Expertise { get; set; }
        public string? IpfsHash { get; set; }
        public IEnumerable<string>? Keywords { get; set; }
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
