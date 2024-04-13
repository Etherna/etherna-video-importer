// Copyright 2022-present Etherna SA
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

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract partial class VideoMetadataBase
    {
        private string? _description;
        private TimeSpan? _duration;
        private string? _originVideoQualityLabel;
        private string? _title;

        // Consts.
        [GeneratedRegex("^(?<label>\\d+p)\\d*$")]
        private static partial Regex QualityLabelRegex();

        // Properties.
        public abstract string Id { get; }
        public bool IsDataFetched => _description is not null &&
                                     _duration is not null &&
                                     _originVideoQualityLabel is not null &&
                                     _title is not null;
        public virtual string Description
        {
            get => _description ?? throw new InvalidOperationException("Description is not fetched");
            protected set => _description = value;
        }
        public TimeSpan Duration
        {
            get => _duration ?? throw new InvalidOperationException("Duration is not fetched");
            protected set => _duration = value;
        }
        public abstract IEnumerable<string> OldIds { get; }
        public string OriginVideoQualityLabel
        {
            get => _originVideoQualityLabel ?? throw new InvalidOperationException("Origin video quality is not fetched");
            protected set
            {
                var match = QualityLabelRegex().Match(value);
                if (match.Success)
                    _originVideoQualityLabel = match.Groups["label"].Value;
                else
                    throw new InvalidOperationException("Invalid quality label");
            }
        }
        public virtual string Title
        {
            get => _title ?? throw new InvalidOperationException("Title is not fetched");
            protected set => _title = value;
        }

        // Methods.
        public virtual Task<bool> TryFetchMetadataAsync() => Task.FromResult(true);
    }
}
