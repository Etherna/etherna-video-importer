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

using System.IO;

namespace Etherna.VideoImporter.Core.Models.Domain.Directories
{
    public abstract class DirectoryBase(string dirPath)
    {
        // Constructors.
        protected DirectoryBase(string parentDirPath, string dirName)
            : this(Path.Combine(parentDirPath, dirName))
        { }

        // Properties.
        public string DirPath { get; } = dirPath;

        // Methods.
        public DirectoryInfo CreateDirectory() =>
            Directory.CreateDirectory(DirPath);
        
        public void Delete() => Directory.Delete(DirPath, true);
        
        public bool Exists() => Directory.Exists(DirPath);
    }
}