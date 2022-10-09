namespace EthernaVideoImporter.Models
{
    internal enum ImportStatus
    {
        NotProcess,
        Downloaded,
        BatchCreated,
        VideoUploaded,
        ThumbnailUploaded,
        MetadataUploaded,
        ReferenceOffer,
        IndexSynced,
        Processed
    }
}
