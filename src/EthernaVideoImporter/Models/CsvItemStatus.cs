namespace Etherna.EthernaVideoImporter.Models
{
    internal enum CsvItemStatus
    {
        Added,
        MetadataModified, //ignored now, causes a lot of reupload
        Unchanged
    }
}
