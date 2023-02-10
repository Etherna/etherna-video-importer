using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public sealed class LinkReporterService : ILinkReporterService
    {
        // Constructors.
        public LinkReporterService()
        {
        }

        // Methods.
        public Task SetEthernaFieldsAsync(
            string destinationUri,
            string ethernaIndex,
            string ethernaPermalink)
        {
            return Task.CompletedTask;
        }
    }
}
