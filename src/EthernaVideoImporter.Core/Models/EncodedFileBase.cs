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

namespace Etherna.VideoImporter.Core.Models
{
    public abstract class EncodedFileBase
    {
        // Constructors.
        protected EncodedFileBase(
            string downloadedFilePath,
            long byteSize,
            TimeSpan duration)
        {
            Bitrate = (int)Math.Ceiling(byteSize * 8 / duration.TotalSeconds);
            ByteSize = byteSize;
            DownloadedFilePath = downloadedFilePath;
            Duration = duration;
        }

        // Properties.
        public int Bitrate { get; }
        public long ByteSize { get; }
        public string DownloadedFilePath { get; }
        public TimeSpan Duration { get; }
        public string? UploadedHashReference { get; set; }
    }
}
