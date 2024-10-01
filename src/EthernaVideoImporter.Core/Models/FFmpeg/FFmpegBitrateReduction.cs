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

using System.Diagnostics.CodeAnalysis;

namespace Etherna.VideoImporter.Core.Models.FFmpeg
{
    [SuppressMessage("Design", "CA1008:Enums should have zero value")]
    [SuppressMessage("Design", "CA1027:Mark enums with FlagsAttribute")]
    public enum FFmpegBitrateReduction
    {
        None = 1,
        Low = 2,
        Normal = 4,
        High = 8,
        Extreme = 16,
        Insane = 32
    }
}