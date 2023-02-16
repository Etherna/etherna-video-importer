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
    public class EncodedVideoFile
    {
        // Constructors.
        public EncodedVideoFile(
            long audioBitrate,
            string downloadedFilePath,
            string name,
            string resolution)
        {
            AudioBitrate = audioBitrate;
            DownloadedFilePath = downloadedFilePath;
            Name = name;
            Resolution = resolution;
        }

        // Properties.
        public long AudioBitrate { get; protected set; }
        public int Bitrate { get; protected set; }
        public int Duration { get; protected set; } // Seconds.
        public string? DownloadedFileName { get; protected set; }
        public string DownloadedFilePath { get; protected set; }
        public string Name { get; protected set; }
        public string Resolution { get; protected set; }
        public long Size { get; protected set; } // Bytes.
        public string? UploadedVideoReference { get; set; }

        // Methods.
        public void SetVideoInfo(
            string filename,
            long fileSize,
            int duration)
        {
            DownloadedFileName = filename;
            Size = fileSize;
            Duration = duration;
            Bitrate = (int)Math.Ceiling((double)fileSize * 8 / duration);
        }
    }
}
