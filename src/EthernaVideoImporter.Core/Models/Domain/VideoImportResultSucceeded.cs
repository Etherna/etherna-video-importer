// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class VideoImportResultSucceeded : VideoImportResultBase
    {
        // Constructor.
        protected VideoImportResultSucceeded(
            VideoMetadataBase sourceMetadata,
            bool isManifestUploaded,
            bool isContentUploaded,
            string indexId,
            string referenceHash) : base(sourceMetadata)
        {
            IndexId = indexId;
            IsManifestUploaded = isManifestUploaded;
            IsContentUploaded = isContentUploaded;
            ReferenceHash = referenceHash;
        }
        
        // Static builders.
        public static VideoImportResultSucceeded FullUploaded(
            VideoMetadataBase sourceMetadata,
            string indexId,
            string referenceHash) =>
            new(sourceMetadata, true, true, indexId, referenceHash);
        
        public static VideoImportResultSucceeded ManifestUpdated(
            VideoMetadataBase sourceMetadata,
            string indexId,
            string referenceHash) =>
            new(sourceMetadata, true, false, indexId, referenceHash);
        
        public static VideoImportResultSucceeded Skipped(
            VideoMetadataBase sourceMetadata,
            string indexId,
            string referenceHash) =>
            new(sourceMetadata, false, false, indexId, referenceHash);

        // Properties.
        public string IndexId { get; }
        public bool IsManifestUploaded { get; }
        public bool IsContentUploaded { get; }
        public string ReferenceHash { get; }
    }
}