using Etherna.VideoImporter.Core.Models.ManifestDtos;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IMigrationService
    {
        MigrationType CalculateOperation(ManifestPersonalDataDto? manifestPersonalDataDto);
    }
}
