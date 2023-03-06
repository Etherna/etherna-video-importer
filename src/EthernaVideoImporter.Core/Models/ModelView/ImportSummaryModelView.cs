namespace Etherna.VideoImporter.Core.Models.ModelView
{
    public class ImportSummaryModelView
    {
        // Properties.
        public int TotDeletedExogenous { get; set; }
        public int TotDeletedRemovedFromSource { get; set; }
        public int TotErrorVideoImported { get; set; }
        public int TotProcessedVideos => TotSuccessVideoImported + TotUpdatedVideoImported + TotSkippedVideoImported + TotErrorVideoImported;
        public int TotSkippedVideoImported { get; set; }
        public int TotSuccessVideoImported { get; set; }
        public int TotUpdatedVideoImported { get; set; }
    }
}
