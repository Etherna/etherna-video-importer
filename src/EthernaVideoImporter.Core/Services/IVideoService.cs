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

using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Uploader services
    /// </summary>
    public interface IVideoService
    {
        /// <summary>
        /// Delete videos uploaded by other tools
        /// </summary>
        /// <param name="video">video data</param>
        /// <param name="pinVideo">pin video</param>
        /// <param name="offerVideo">free video</param>
        Task DeleteExogenousVideosAsync(
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos);

        /// <summary>
        /// Delete videos removed from source
        /// </summary>
        /// <param name="videosMetadataFromSource">Videos metadata from source</param>
        /// <param name="indexedVideos">Indexed videos</param>
        /// <param name="gatewayPinnedHashes">Gateway pinned hashes</param>
        /// <param name="unpinRemovedVideos">Unpin removed videos</param>
        Task DeleteVideosRemovedFromSourceAsync(
            IEnumerable<VideoMetadataBase> videosMetadataFromSource,
            IEnumerable<IndexedVideo> indexedVideos,
            IEnumerable<string>? gatewayPinnedHashes,
            bool unpinRemovedVideos);

        /// <summary>
        /// Pin all reference
        /// </summary>
        /// <param name="indexedVideoManifest">Indexed video manifest</param>
        /// <param name="manifestPermalinkHash">Manifest permalink hash</param>
        public Task PinVideoAsync(IndexedVideoManifest indexedVideoManifest, string manifestPermalinkHash);
        
        /// <summary>
        /// Pin all reference
        /// </summary>
        /// <param name="video">video data</param>
        public Task PinVideoAsync(Video video);

        /// <summary>
        /// Start to upload all video data (manifest, video with all avaiable resolutions, thumbnail, index).
        /// </summary>
        /// <param name="video">video data</param>
        /// <param name="pinVideo">pin video</param>
        /// <param name="offerVideo">free video</param>
        public Task UploadVideoAsync(
            Video video,
            bool pinVideo,
            bool offerVideo);

        /// <summary>
        /// Upload a new video manifest
        /// </summary>
        /// <param name="videoManifest">The video manifest</param>
        /// <param name="pinManifest">True if need to pin manifest</param>
        /// <returns>The new manifest hash</returns>
        Task<string> UploadVideoManifestAsync(
            ManifestDto videoManifest,
            bool pinManifest);
    }
}
