//   Copyright 2022-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

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
