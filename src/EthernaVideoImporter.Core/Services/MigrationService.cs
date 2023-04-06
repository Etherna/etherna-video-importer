using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;

namespace Etherna.VideoImporter.Core.Services
{
    public class MigrationService : IMigrationService
    {
        public OperationType DecideOperation(ManifestPersonalDataDto? manifestPersonalDataDto)
        {
            // If client version is missing (0.1.x or 0.2.x).
            if (string.IsNullOrWhiteSpace(manifestPersonalDataDto?.ClientVersion))
                return OperationType.ImportAll;

            return new Version(manifestPersonalDataDto.ClientVersion) switch
            {
                { Major: 0, Minor: <= 2 } => OperationType.ImportAll,
                _ => OperationType.Skip
            };
        }
    }
}
