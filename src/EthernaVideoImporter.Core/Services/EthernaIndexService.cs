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
using Etherna.Sdk.Users;
using Etherna.VideoImporter.Core.Models.Domain;
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using Etherna.VideoImporter.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public class EthernaIndexService : IEthernaIndexService
    {
        // Consts.
        private const int MaxVideosForPage = 100;
        
        // Fields.
        private readonly IEthernaUserIndexClient ethernaIndexClient;
        private readonly EthernaIndexServiceOptions options;

        // Constructor.
        public EthernaIndexService(
            IEthernaUserIndexClient ethernaIndexClient,
            IOptions<EthernaIndexServiceOptions> options)
        {
            ArgumentNullException.ThrowIfNull(ethernaIndexClient, nameof(ethernaIndexClient));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            this.ethernaIndexClient = ethernaIndexClient;
            this.options = options.Value;

            ActiveIndexes = this.options.IndexUrls.Select(url => new EthernaIndex(url));
        }
        
        // Properties.
        public IEnumerable<EthernaIndex> ActiveIndexes { get; }
        
        // Methods.
        public Task<string> AddVideoAsync(string hash, EthernaIndex index) =>
            ethernaIndexClient.VideosClient.VideosPostAsync(new VideoCreateInput { ManifestHash = hash });

        public Task DeleteVideoAsync(IndexedVideo video)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));
            
            return ethernaIndexClient.VideosClient.VideosDeleteAsync(video.VideoId);
        }

        public async Task<IEnumerable<IndexedVideo>> GetUserVideosAsync(string userAddress, EthernaIndex index)
        {
            var videos = new List<VideoDto>();

            VideoDtoPaginatedEnumerableDto? page = null;
            do
            {
                page = await ethernaIndexClient.UsersClient.Videos2Async(
                    userAddress,
                    page is null ? 0 : page.CurrentPage + 1,
                    MaxVideosForPage);
                videos.AddRange(page.Elements);
            } while (page.Elements.Count != 0);

            return videos.Select(v => new IndexedVideo(index, v));
        }

        public async Task RefreshParametersAsync(EthernaIndex index)
        {
            ArgumentNullException.ThrowIfNull(index, nameof(index));
            
            var parametersDto = await ethernaIndexClient.SystemClient.ParametersAsync();
            index.Parameters = new EthernaIndexParameters(
                parametersDto.CommentMaxLength,
                parametersDto.VideoDescriptionMaxLength,
                parametersDto.VideoPersonalDataMaxLength,
                parametersDto.VideoTitleMaxLength);
        }

        public IEnumerable<IndexedVideo> SearchIndexedVideos(
            VideoMetadataBase videoMetadata,
            IEnumerable<IndexedVideo> indexedVideos)
        {
            ArgumentNullException.ThrowIfNull(videoMetadata, nameof(videoMetadata));

            // Extract all possible Id hashes.
            var allVideoIds = videoMetadata.OldIds.Append(videoMetadata.Id);
            var allVideoIdHashes = allVideoIds.Select(id => ManifestPersonalDataDto.HashVideoId(id));
            
            // Search for previously published videos. Compare with video manifest's personal data.
            return indexedVideos.Where(v => v.LastValidManifest?.PersonalData?.VideoIdHash is not null &&
                                            allVideoIdHashes.Contains(v.LastValidManifest.PersonalData.VideoIdHash));
        }

        public Task<VideoManifestDto> UpdateVideoAsync(string videoId, string newHash, EthernaIndex index) =>
            ethernaIndexClient.VideosClient.VideosPutAsync(videoId, newHash);
    }
}