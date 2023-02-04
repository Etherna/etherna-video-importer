using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    /// <summary>
    /// Link services
    /// </summary>
    public interface ILinkReporterService
    {
        /// <summary>
        /// Set the url for .MD file
        /// </summary>
        /// <param name="ethernaIndex">Url to index</param>
        /// <param name="ethernaPermalink">Url to permalink</param>
        Task SetEthernaFieldsAsync(
            string ethernaIndex,
            string ethernaPermalink);
    }
}
