namespace Etherna.VideoImporter.Core.Options
{
    public class CacheServiceOptions
    {
        // Properties.
        public string CacheFolderPath { get; set; } = CommonConsts.DefaultCacheFolder;
        public bool CacheEnable { get; set; }
    }
}
