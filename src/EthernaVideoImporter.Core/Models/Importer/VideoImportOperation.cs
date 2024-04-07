// Copyright 2022-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.Importer
{
    /// <summary>
    /// Represent a full video import operation. It has a state and include different phases.
    /// </summary>
    public class VideoImportOperation
    {
        // Fields.
        private readonly List<VideoImportOperationStage> _tracedStates = new();
        private readonly bool forceFullUpload;

        // Constructor.
        public VideoImportOperation(
            VideoMetadataBase sourceMetadata,
            IEnumerable<IndexedVideo> userIndexedVideos,
            bool forceFullUpload)
        {
            ArgumentNullException.ThrowIfNull(sourceMetadata, nameof(sourceMetadata));

            this.forceFullUpload = forceFullUpload;
            Video = new Video(sourceMetadata, userIndexedVideos);
        }
        
        // Properties.
        public bool IsCompleted => _tracedStates.Contains(VideoImportOperationStage.Failed) ||
                                   _tracedStates.Contains(VideoImportOperationStage.Succeeded);
        
        public bool IsNewManifestUploaded { get; }
        
        public bool IsNewThumbnailUploaded { get; }
        
        public bool IsNewVideoStreamsUploaded { get; }
        
        /// <summary>
        /// True if manifest needs to be uploaded.
        /// </summary>
        public bool IsUploadManifestRequired
        {
            get
            {
                // Check if has been uploaded.
                if (IsNewManifestUploaded)
                    return false;
                
                // Check if full upload is forced.
                if (forceFullUpload)
                    return true;
                
                // Check for dependencies.
                if (IsUploadThumbnailRequired || IsUploadVideoStreamsRequired)
                    return true;
                
                // Check if exists and updated manifest.
                return !Video.IndexedUpdated.Any();
            }
        }
        
        /// <summary>
        /// True if thumbnail needs to be uploaded.
        /// </summary>
        public bool IsUploadThumbnailRequired
        {
            get
            {
                // Check if full upload is forced.
                if (forceFullUpload)
                    return true;

                // Check for technical requirements.
                return AlreadyIndexed.All(v => v.LastValidManifest?.HasLastSpecifications != true);
                
                // Check if data changed.
                //how? Need to understand.
            }
        }
        
        /// <summary>
        /// True if video streams needs to be uploaded.
        /// </summary>
        public bool IsUploadVideoStreamsRequired 
        {
            get
            {
                if ()
                {
                    
                }
                
                // Check if full upload is forced.
                if (forceFullUpload)
                    return true;

                // Check for technical requirements.
                return AlreadyIndexed.All(v => v.LastValidManifest?.HasLastSpecifications != true);
            }
        }

        public bool Succeeded => _tracedStates.Contains(VideoImportOperationStage.Succeeded);

        public IEnumerable<VideoImportOperationStage> TracedStates => _tracedStates;
        
        public Video Video { get; }
        
        // Methods.
        public void TraceOperationStage(VideoImportOperationStage stage)
        {
            if (IsCompleted)
                throw new InvalidOperationException();
            _tracedStates.Add(stage);
        }
    }
}