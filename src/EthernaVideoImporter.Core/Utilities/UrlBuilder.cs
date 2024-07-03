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
using System.Diagnostics.CodeAnalysis;

namespace Etherna.VideoImporter.Core.Utilities
{
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings")]
    public static class UrlBuilder
    {
        // Consts.
        public const string EmbeddedUrlPrefix = "https://etherna.io/embed/";
        public const string NormalUrlPrefix = "https://etherna.io/watch/";
        
        // Static methods.
        public static string BuildEmbeddedIndexUrl(string indexId) => EmbeddedUrlPrefix + indexId;
        public static string BuildEmbeddedPermalinkUrl(SwarmHash referenceHash) => EmbeddedUrlPrefix + referenceHash;
        public static string BuildNormalIndexUrl(string indexId) => NormalUrlPrefix + indexId;
        public static string BuildNormalPermalinkUrl(SwarmHash referenceHash) => NormalUrlPrefix + referenceHash;
    }
}