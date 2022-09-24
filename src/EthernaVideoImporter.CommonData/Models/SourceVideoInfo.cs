namespace EthernaVideoImporter.CommonData.Models
{
    public class SourceVideoInfo
    {
        // Contractor.
        public SourceVideoInfo(
            int audioBitrate,
            string filename,
            int resolution,
            string? videoId,
            string uri)
        {
            AudioBitrate = audioBitrate;
            Filename = filename;
            Resolution = resolution;
            VideoId = videoId;
            Uri = uri;
        }

        // Properties.
        public int AudioBitrate { get; set; }
        public string Filename { get; set; }
        public int Resolution { get; set; }
        public string? VideoId { get; set; }
        public string Uri { get; set; }
    }
}
