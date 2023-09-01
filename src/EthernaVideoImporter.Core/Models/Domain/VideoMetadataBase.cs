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

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract partial class VideoMetadataBase
    {
        // Consts.
        [GeneratedRegex("^(?<label>\\d+p)\\d*$")]
        private static partial Regex QualityLabelRegex();

        // Constructor.
        protected VideoMetadataBase(
            string title,
            string description,
            TimeSpan duration,
            string originVideoQualityLabel)
        {
            Description = description;
            Duration = duration;

            var originVideoQualityLabelMatch = QualityLabelRegex().Match(originVideoQualityLabel);
            if (originVideoQualityLabelMatch.Success)
                OriginVideoQualityLabel = originVideoQualityLabelMatch.Groups["label"].Value;
            else
                throw new ArgumentException("Invalid quality label", nameof(originVideoQualityLabel));
            
            Title = title;
        }

        // Properties.
        public abstract string Id { get; }
        public string Description { get; }
        public TimeSpan Duration { get; }
        public abstract IEnumerable<string> OldIds { get; }
        public string OriginVideoQualityLabel { get; }
        public string Title { get; }
    }
}
