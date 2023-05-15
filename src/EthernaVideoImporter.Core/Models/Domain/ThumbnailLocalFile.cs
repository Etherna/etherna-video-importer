﻿//   Copyright 2022-present Etherna Sagl
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

using Blurhash.SkiaSharp;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class ThumbnailLocalFile : LocalFileBase, IThumbnailFile
    {
        // Consts.
        public static readonly int[] ThumbnailResponsiveSizes = { 480, 960, 1280 };

        // Constructor.
        public ThumbnailLocalFile(
            string filePath,
            long byteSize,
            int height,
            int width)
            : base(filePath, byteSize)
        {
            using var thumbFileStream = File.OpenRead(filePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
            Blurhash = Blurhasher.Encode(thumbBitmap, 4, 4);
            Height = height;
            Width = width;
        }

        // Properties.
        /// <summary>
        /// Canvas area (width multiplied by height).
        /// </summary>
        public int Area => Width * Height;

        public float AspectRatio => (float)Width / Height;

        public string Blurhash { get; }

        /// <summary>
        /// Canvas height (in pixels).
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; }

        // Methods.
        public async Task<IEnumerable<ThumbnailLocalFile>> GetScaledThumbnailsAsync(
            DirectoryInfo importerTempDirectoryInfo)
        {
            if (importerTempDirectoryInfo is null)
                throw new ArgumentNullException(nameof(importerTempDirectoryInfo));

            List<ThumbnailLocalFile> thumbnails = new();

            using var thumbFileStream = File.OpenRead(FilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            foreach (var responsiveWidthSize in ThumbnailResponsiveSizes)
            {
                var responsiveHeightSize = (int)(responsiveWidthSize / AspectRatio);
                var thumbnailResizedPath = Path.Combine(importerTempDirectoryInfo.FullName, $"thumb_{responsiveWidthSize}_{responsiveHeightSize}_{Guid.NewGuid()}.jpg");

                using (SKBitmap scaledBitmap = thumbBitmap.Resize(new SKImageInfo(responsiveWidthSize, responsiveHeightSize), SKFilterQuality.Medium))
                using (SKImage scaledImage = SKImage.FromBitmap(scaledBitmap))
                using (SKData data = scaledImage.Encode())
                using (FileStream outputFileStream = new(thumbnailResizedPath, FileMode.CreateNew))
                {
                    await data.AsStream().CopyToAsync(outputFileStream);
                }

                thumbnails.Add(new ThumbnailLocalFile(
                    thumbnailResizedPath,
                    new FileInfo(thumbnailResizedPath).Length,
                    responsiveHeightSize,
                    responsiveWidthSize));
            }

            return thumbnails;
        }
    }
}
