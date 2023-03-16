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

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class ThumbnailLocalFile : LocalFileBase
    {
        // Consts.
        public static readonly int[] ThumbnailResponsiveSizes = { 480, 960, 1280 };

        public ThumbnailLocalFile(
            string downloadedFilePath,
            long byteSize,
            int width,
            int height)
            : base(downloadedFilePath, byteSize)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Canvas height (in pixels).
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Canvas area (width multiplied by height).
        /// </summary>
        public int Area => Width * Height;
    }
}
