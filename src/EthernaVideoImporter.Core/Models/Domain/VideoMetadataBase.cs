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

using Etherna.VideoImporter.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public abstract class VideoMetadataBase(IVideoProvider videoProvider)
    {
        // Fields.
        private string? _description;
        private TimeSpan? _duration;
        private string? _title;

        // Properties.
        public IEnumerable<string> AllSourceIds => SourceOldIds.Prepend(SourceId);
        public bool IsDataFetched => _description is not null &&
                                     _duration is not null &&
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
        public abstract string SourceId { get; }
        public string SourceName => videoProvider.SourceName;
        public abstract IEnumerable<string> SourceOldIds { get; }
        public virtual string Title
        {
            get => _title ?? throw new InvalidOperationException("Title is not fetched");
            protected set => _title = value;
        }

        // Methods.
        public virtual Task<bool> TryFetchMetadataAsync(IIoService ioService) => Task.FromResult(true);
    }
}
