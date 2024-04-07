//   Copyright 2022-present Etherna SA
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
using Etherna.VideoImporter.Core.Models.Index;
using Etherna.VideoImporter.Core.Models.ManifestDtos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class Video
    {
        // Fields.
        private readonly Dictionary<EthernaIndex, IndexedVideo> _indexedNewByIndex = new();
        
        // Constructor.
        public Video(
            VideoMetadataBase metadata,
            IEnumerable<IndexedVideo>? userIndexedVideos = null)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            
            // Select old published video versions, by possible source Ids.
            IndexedOld = (userIndexedVideos ?? Array.Empty<IndexedVideo>()).Where(
                v => v.LastValidManifest?.PersonalData?.VideoIdHash is not null &&
                     metadata.AllSourceIdHashes.Contains(v.LastValidManifest.PersonalData.VideoIdHash)).ToArray();
            
            // Try initialize props with updated info from old indexed.
            var lastIndexed = IndexedUpdated.MaxBy(i => i.CreationDateTime);
            if (lastIndexed != null)
            {
                EncodedFiles = lastIndexed.LastValidManifest!.Sources
                    .Select<SourceDto, FileBase>(s => new VideoSwarmFile(s.Size, s.Quality, s.Reference)).ToList();
                ManifestHash = lastIndexed.LastValidManifest.Hash;
                ThumbnailFiles = lastIndexed.LastValidManifest.Thumbnail.Sources
                    .Select<KeyValuePair<string, string>, IThumbnailFile>(s => new ThumbnailSwarmFile(
                        lastIndexed.LastValidManifest.Thumbnail.AspectRatio,
                        lastIndexed.LastValidManifest.Thumbnail.Blurhash,
                        s.Value,
                        0,
                        int.Parse(s.Key.Trim('w'), CultureInfo.InvariantCulture))).ToList();
            }
        }

        // Properties.
        public IList<FileBase> EncodedFiles { get; } = new List<FileBase>();
        public string? ManifestHash { get; set; }
        public IEnumerable<IndexedVideo> IndexedNew => _indexedNewByIndex.Values;
        public IEnumerable<IndexedVideo> IndexedOld { get; }
        public IEnumerable<IndexedVideo> IndexedUpdated => LatestIndexedByIndex.Values.Where(v =>
            v.LastValidManifest is not null &&
            v.LastValidManifest.HasLastSpecifications &&
            v.LastValidManifest.HasEqualMetadata(Metadata)
            /* don't consider data stream */);
        public IDictionary<EthernaIndex, IndexedVideo> LatestIndexedByIndex =>
            IndexedNew.UnionBy(IndexedOld, i => i.Index) //union order is relevant
                .ToDictionary(i => i.Index);
        public VideoMetadataBase Metadata { get; }
        public IList<IThumbnailFile> ThumbnailFiles { get; } = new List<IThumbnailFile>();

        // Methods.
        public void AddOrReplaceIndexed(IndexedVideo indexedVideo)
        {
            ArgumentNullException.ThrowIfNull(indexedVideo, nameof(indexedVideo));
            _indexedNewByIndex[indexedVideo.Index] = indexedVideo;
        }

        public async Task<long> GetTotalByteSizeAsync()
        {
            long totalByteSize = 0;

            foreach (var file in EncodedFiles)
                totalByteSize += await file.GetByteSizeAsync();
            foreach (var file in ThumbnailFiles)
                totalByteSize += await file.GetByteSizeAsync();
            totalByteSize += JsonSerializer.Serialize(await ManifestDto.BuildNewAsync(
                this,
                CommonConsts.SwarmNullReference,
                CommonConsts.EthereumNullAddress,
                allowFakeReferences: true)).Length;

            return totalByteSize;
        }
    }
}
