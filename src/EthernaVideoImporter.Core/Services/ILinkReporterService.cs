using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Link services
    /// </summary>
    public interface ILinkReporterService
    {
        /// <summary>
        /// Set etherna data in destination Uri.
        /// </summary>
        /// <param name="destinationUri">Uri to save the etherna data</param>
        /// <param name="ethernaIndex">Url to index</param>
        /// <param name="ethernaPermalink">Url to permalink</param>
        Task SetEthernaFieldsAsync(
            string destinationUri,
            string ethernaIndex,
            string ethernaPermalink);
    }
}
