using Etherna.VideoImporter.Core.Models.ManifestDtos;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IMigrationService
    {
        OperationType DecideOperation(ManifestPersonalDataDto? manifestPersonalDataDto);
    }
}
