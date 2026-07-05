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

using Etherna.SwarmSdk.Models;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class VideoImportResultSucceeded : VideoImportResultBase
    {
        // Constructor.
        private VideoImportResultSucceeded(
            VideoMetadataBase sourceMetadata,
            bool isManifestUploaded,
            bool isContentUploaded,
            string indexId,
            SwarmReference reference)
            : base(sourceMetadata)
        {
            IndexId = indexId;
            IsManifestUploaded = isManifestUploaded;
            IsContentUploaded = isContentUploaded;
            Reference = reference;
        }
        
        // Static builders.
        public static VideoImportResultSucceeded FullUploaded(
            VideoMetadataBase sourceMetadata,
            string indexId,
            SwarmReference reference) =>
            new(sourceMetadata, true, true, indexId, reference);
        
        public static VideoImportResultSucceeded ManifestUpdated(
            VideoMetadataBase sourceMetadata,
            string indexId,
            SwarmReference reference) =>
            new(sourceMetadata, true, false, indexId, reference);
        
        public static VideoImportResultSucceeded Skipped(
            VideoMetadataBase sourceMetadata,
            string indexId,
            SwarmReference reference) =>
            new(sourceMetadata, false, false, indexId, reference);

        // Properties.
        public string IndexId { get; }
        public bool IsManifestUploaded { get; }
        public bool IsContentUploaded { get; }
        public SwarmReference Reference { get; }
    }
}