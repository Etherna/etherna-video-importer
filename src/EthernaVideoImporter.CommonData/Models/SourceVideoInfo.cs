namespace Etherna.EthernaVideoImporter.Dtos
{
    public class SourceVideoInfo
    {
        // Contractor.
        public SourceVideoInfo(
            int audioBitrate,
            string filename,
            int resolution,
            string uri)
        {
            AudioBitrate = audioBitrate;
            Filename = filename;
            Resolution = resolution;
            Uri = uri;
        }

        // Properties.
        public int AudioBitrate { get; set; }
        public string Filename { get; set; }
        public int Resolution { get; set; }
        public string Uri { get; set; }
    }
}
