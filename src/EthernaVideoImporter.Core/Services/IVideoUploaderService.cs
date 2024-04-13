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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Uploader services
    /// </summary>
    public interface IVideoUploaderService
    {
        /// <summary>
        /// Start to upload all video data (manifest, video with all avaiable resolutions, thumbnail, index).
        /// </summary>
        /// <param name="video">Video data</param>
        /// <param name="pinVideo">Pin video</param>
        /// <param name="offerVideo">Offer video contents for free</param>
        public Task UploadVideoAsync(
            Video video,
            bool pinVideo,
            bool offerVideo,
            string userEthAddress);

        /// <summary>
        /// Upload a new video manifest
        /// </summary>
        /// <param name="videoManifest">The video manifest</param>
        /// <param name="pinManifest">True if need to pin manifest</param>
        /// <param name="offerManifest">Offer manifest for free</param>
        /// <returns>The new manifest hash</returns>
        Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest,
            bool offerManifest);
    }
}
