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
using Etherna.VideoImporter.Core.Services;
using M3U8Parser;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class VideoVariantBase(
        int height,
        int width)
    {
        // Properties.
        public int Height { get; } = height;
        public abstract FileBase EntryFile { get; }
        public string QualityLabel => $"{Height}p";
        public int Width { get; } = width;
        public abstract long TotalByteSize { get; }
    }
}