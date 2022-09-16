namespace Etherna.EthernaVideoImporter.Dtos
{
    public class VideoDownloadInfo
    {
        // Constructors.
        public VideoDownloadInfo(
            int bitrate,
            string quality,
            long size)
        {
            Bitrate = bitrate;
            Quality = quality;
            Size = size;
        }

        // Properties.
        public int Bitrate { get; }
        public string Quality { get; }
        public long Size { get; }
    }
}
