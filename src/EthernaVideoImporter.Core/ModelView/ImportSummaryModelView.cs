namespace Etherna.VideoImporter.Core.ModelView
{
    public class ImportSummaryModelView
    {
        // Properties.
        public int DeletedExogenous { get; set; }
        public int DeleteOldSource { get; set; }
        public int ErrorVideoImported { get; set; }
        public int SuccessfullyImported { get; set; }
        public int SkippedVideoImported { get; set; }
        public int TotalVideo
        {
            get
            {
                return SuccessfullyImported + UpdatedVideoImported + SkippedVideoImported + ErrorVideoImported;
            }
        }
        public int UpdatedVideoImported { get; set; }
    }
}
