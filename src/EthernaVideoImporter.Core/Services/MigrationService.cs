// Copyright 2022-present Etherna SA
// This file is part of Etherna Video Importer.
// 
// Etherna Video Importer is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Etherna Video Importer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Etherna Video Importer.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Sdk.Users.Index.Models;
using System;

namespace Etherna.VideoImporter.Core.Services
{
    public class MigrationService : IMigrationService
    {
        public OperationType DecideOperation(VideoManifestPersonalData? personalData)
        {
            // If client version is missing (0.1.x or 0.2.x).
            if (string.IsNullOrWhiteSpace(personalData?.ClientVersion))
                return OperationType.ImportAll;

            return new Version(personalData.ClientVersion) switch
            {
                { Major: 0, Minor: <= 2 } => OperationType.ImportAll,
                { Major: 0, Minor: 3, Revision: < 9} => OperationType.ImportAll,
                _ => OperationType.Skip
            };
        }
    }
}
