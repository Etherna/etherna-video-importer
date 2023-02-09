using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporterLibrary.Services
{
    public sealed class LinkReporterService : ILinkReporterService
    {
        // Constructors.
        public LinkReporterService()
        {
        }

        // Methods.
        public Task SetEthernaFieldsAsync(
            string ethernaIndex,
            string ethernaPermalink)
        {
            return Task.CompletedTask;
        }
    }
}
