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

using Etherna.BeeNet.Models;
using Etherna.UniversalFiles;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class SourceFile(
        UniversalFile universalFile)
        : FileBase
    {
        // Properties.
        public UniversalUri FileUri => universalFile.FileUri;
        
        // Methods.
        public override Task<long> GetByteSizeAsync() => universalFile.GetByteSizeAsync();

        public async Task<Stream> ReadToStreamAsync() => (await universalFile.ReadToStreamAsync()).Stream;

        public Task<string> ReadToStringAsync() => universalFile.ReadToStringAsync();

        public void SetSwarmHash(SwarmHash hash)
        {
            if (SwarmHash != null)
                throw new InvalidOperationException("Swarm hash already set");
            SwarmHash = hash;
        }
    }
}
