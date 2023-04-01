using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;

namespace Etherna.VideoImporter.Core.Services
{
    public class MigrationService : IMigrationService
    {
        public MigrationType CalculateOperation(ManifestPersonalDataDto? manifestPersonalDataDto)
        {
            if (manifestPersonalDataDto is null)
                return MigrationType.Nothing;

            if (string.IsNullOrWhiteSpace(manifestPersonalDataDto.ClientVersion))
                return MigrationType.Import;

            var manifestVersion = new Version(manifestPersonalDataDto.ClientVersion);
            if (manifestVersion.MajorRevision == 0 &&
                manifestVersion.MinorRevision <= 2)
                return MigrationType.Import;

            return MigrationType.Nothing;
        }
    }
}
