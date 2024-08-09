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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class VideoEncodingBase
    {
        // Constructor.
        protected VideoEncodingBase(
            TimeSpan duration,
            FileBase? masterFile,
            VideoVariantBase[] variants)
        {
            ArgumentNullException.ThrowIfNull(variants, nameof(variants));
            if (variants.Length == 0)
                throw new ArgumentException("Variant list can't be empty");

            Duration = duration;
            MasterFile = masterFile;
            Variants = variants;
        }

        // Properties.
        public TimeSpan Duration { get; }
        public FileBase? MasterFile { get; }
        public long TotalByteSize => (MasterFile?.ByteSize ?? 0) + Variants.Sum(v => v.TotalByteSize);
        public IReadOnlyCollection<VideoVariantBase> Variants { get; }
    }
}