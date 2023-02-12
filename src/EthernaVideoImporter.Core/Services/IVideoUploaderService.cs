//   Copyright 2022-present Etherna Sagl
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

using Etherna.ServicesClient.Clients.Index;
using Etherna.VideoImporter.Core.Models;
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
        /// <param name="videoData">all video data</param>
        /// <param name="pinVideo">pin video</param>
        /// <param name="offerVideo">free video</param>
        public Task UploadVideoAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo);

        /// <summary>
        /// Update metadata and index.
        /// </summary>
        /// <param name="videoManifestDto">manifest data</param>
        /// <param name="videoData">video data</param>
        /// <param name="pinVideo">free video</param>
        Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool pinVideo);
    }
}
