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

using Etherna.Sdk.GeneratedClients.Index;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IEthernaIndexService
    {
        // Properties.
        IEnumerable<EthernaIndex> ActiveIndexes { get; }
        
        // Methods.
        Task<string> AddVideoAsync(VideoCreateInput createInput, EthernaIndex index);

        Task DeleteVideoAsync(IndexedVideo video);

        Task<IEnumerable<IndexedVideo>> GetUserVideosAsync(string userAddress, EthernaIndex index);

        Task RefreshParametersAsync(EthernaIndex index);

        IEnumerable<IndexedVideo> SearchIndexedVideos(
            VideoMetadataBase videoMetadata,
            IEnumerable<IndexedVideo> indexedVideos);

        Task<VideoManifestDto> UpdateVideoAsync(string videoId, string newHash);
    }
}