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
using Etherna.VideoImporter.Core.Models.Domain;
using System;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestVideoSourceDto
    {
        public ManifestVideoSourceDto(SourceDto source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            Quality = source.Quality;
            Reference = source.Reference;
            Size = source.Size;
        }

        public ManifestVideoSourceDto(VideoLocalFile videoFile)
        {
            if (videoFile is null)
                throw new ArgumentNullException(nameof(videoFile));

            Quality = videoFile.VideoQualityLabel;
            Reference = videoFile.UploadedHashReference ?? throw new InvalidOperationException();
            Size = videoFile.ByteSize;
        }

        public int Bitrate => 420; //fake place holder, will be removed on manifest 2.0
        public string Quality { get; }
        public string Reference { get; }
        public long Size { get; }
    }
}
