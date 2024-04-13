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

using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestVideoSourceDto
    {
        // Constructors.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private ManifestVideoSourceDto() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Builders.
        public static async Task<ManifestVideoSourceDto> BuildNewAsync(
            IVideoFile videoFile,
            bool allowFakeReference)
        {
            ArgumentNullException.ThrowIfNull(videoFile, nameof(videoFile));

            return new ManifestVideoSourceDto
            {
                Quality = videoFile.VideoQualityLabel,
                Reference = videoFile.SwarmHash ?? (allowFakeReference ?
                    CommonConsts.SwarmNullReference :
                    throw new InvalidOperationException()),
                Size = await videoFile.GetByteSizeAsync()
            };
        }

        // Properties.
        public int Bitrate => 420; //fake place holder, will be removed on manifest 2.0
        public string Quality { get; private set; }
        public string Reference { get; private set; }
        public long Size { get; private set; }
    }
}
