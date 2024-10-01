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
using Etherna.VideoImporter.Core.Models.Domain;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    /// <summary>
    /// Uploader services
    /// </summary>
    public interface IVideoUploaderService
    {
        /// <summary>
        /// Start to upload all video data (manifest, video with all available resolutions, thumbnail, index).
        /// </summary>
        /// <param name="video">Video data</param>
        /// <param name="fundPinning">Pin video</param>
        /// <param name="fundDownload">Offer video contents for free</param>
        /// <param name="ownerEthAddress">Owner eth address</param>
        /// <param name="batchId">Use existing batch id</param>
        public Task UploadVideoAsync(
            Video video,
            bool fundPinning,
            bool fundDownload,
            string ownerEthAddress,
            PostageBatchId? batchId = null);
    }
}
