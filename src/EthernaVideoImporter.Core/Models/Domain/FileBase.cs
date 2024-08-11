﻿// Copyright 2022-present Etherna SA
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
    public class FileBase(
        long byteSize,
        string fileName,
        UFile universalFile,
        SwarmHash? swarmHash)
    {
        // Static builders.
        public static async Task<FileBase> BuildFromUFileAsync(UFile uFile)
        {
            ArgumentNullException.ThrowIfNull(uFile, nameof(uFile));
            
            var fileSize = await uFile.GetByteSizeAsync();
            var fileName = await uFile.TryGetFileNameAsync() ?? throw new InvalidOperationException(
                $"Can't get file name from {uFile.FileUri.OriginalUri}");
            
            return new FileBase(fileSize, fileName,uFile, null);
        }
        
        // Properties.
        public long ByteSize { get; } = byteSize;
        public string FileName { get; } = fileName;
        public SwarmHash? SwarmHash { get; set; } = swarmHash;
        public UUri UUri => universalFile.FileUri;
        
        // Methods.
        public async Task<Stream> ReadToStreamAsync() => (await universalFile.ReadToStreamAsync()).Stream;

        public Task<string> ReadToStringAsync() => universalFile.ReadToStringAsync();
    }
}
