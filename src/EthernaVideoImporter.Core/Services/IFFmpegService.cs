﻿//   Copyright 2022-present Etherna SA
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
using Etherna.VideoImporter.Core.Models.FFmpegDto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Services
{
    public interface IFFmpegService
    {
        // Methods.
        Task<string> CreateRandomThumbnailAsync(
            VideoSourceFile videoSourceFile);
        Task<IEnumerable<(string filePath, int height, int width)>> EncodeVideosAsync(
            VideoSourceFile sourceVideoFile,
            IEnumerable<int> outputHeights);
        Task<string> GetFFmpegBinaryPathAsync();
        Task<string> GetFFprobeBinaryPathAsync();
        Task<FFProbeResultDto> GetVideoInfoAsync(string videoFileAbsoluteUri);
    }
}