using System.Collections.Generic;

namespace Etherna.VideoImporter.Devcon.MdDto
{
    internal sealed class ArchiveMdFileDto
    {
        // Properties.
        public string Description { get; set; } = default!;
        public string? EthernaIndex { get; set; }
        public string? EthernaPermalink { get; set; }
        public string MdFilePath { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string YoutubeUrl { get; set; } = default!;

        // Methods.
        public void AddDescription(IEnumerable<string> descriptions)
        {
            Description ??= "";
            Description += string.Join(". ", descriptions);
        }
    }
}
