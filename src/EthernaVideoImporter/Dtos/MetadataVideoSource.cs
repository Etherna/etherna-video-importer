namespace Etherna.EthernaVideoImporter.Dtos
{
    public class MetadataVideoSource
    {
        // Constructors.
        public MetadataVideoSource(
            int bitrate,
            string quality,
            string reference,
            long size)
        {
            Bitrate = bitrate;
            Quality = quality;
            Reference = reference;
            Size = size;
        }

        // Properties.
        public int Bitrate { get; }
        public string Quality { get; }
        public string Reference { get; }
        public long Size { get; }
    }
}
