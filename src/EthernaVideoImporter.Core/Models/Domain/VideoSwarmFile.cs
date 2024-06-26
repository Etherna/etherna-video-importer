﻿// Copyright 2022-present Etherna SA
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

using Etherna.BeeNet.Models;
using System;
using System.Text.RegularExpressions;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public partial class VideoSwarmFile : SwarmFileBase, IVideoFile
    {
        // Consts.
        [GeneratedRegex("^(?<label>\\d+p)\\d*$")]
        private static partial Regex QualityLabelRegex();

        // Constructor.
        public VideoSwarmFile(
            long byteSize,
            string videoQualityLabel,
            SwarmHash hash)
            : base(hash, byteSize)
        {
            var originVideoQualityLabelMatch = QualityLabelRegex().Match(videoQualityLabel);
            if (originVideoQualityLabelMatch.Success)
                VideoQualityLabel = originVideoQualityLabelMatch.Groups["label"].Value;
            else
                throw new ArgumentException("Invalid quality label", nameof(videoQualityLabel));
        }

        // Properties.
        public string VideoQualityLabel { get; }
    }
}
