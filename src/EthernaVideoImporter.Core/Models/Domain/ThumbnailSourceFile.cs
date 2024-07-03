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

using Blurhash.SkiaSharp;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class ThumbnailSourceFile : SourceFile, IThumbnailFile
    {
        // Consts.
        public static readonly int[] ThumbnailResponsiveSizes = { 480, 960, 1280 };

        // Constructor.
        private ThumbnailSourceFile(
            SourceUri fileUri,
            IHttpClientFactory httpClientFactory)
            : base(fileUri, httpClientFactory)
        { }

        // Static builders.
        public static async Task<ThumbnailSourceFile> BuildNewAsync(
            SourceUri fileUri,
            IHttpClientFactory httpClientFactory)
        {
            var thumbnail = new ThumbnailSourceFile(fileUri, httpClientFactory);

            using var thumbFileStream = (await thumbnail.ReadToStreamAsync()).Stream;
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            thumbnail.Blurhash = Blurhasher.Encode(thumbBitmap, 4, 4);
            thumbnail.Height = thumbBitmap.Height;
            thumbnail.Width = thumbBitmap.Width;

            return thumbnail;
        }

        // Properties.
        /// <summary>
        /// Canvas area (width multiplied by height).
        /// </summary>
        public int Area => Width * Height;

        public float AspectRatio => (float)Width / Height;

        public string Blurhash { get; private set; } = default!;

        /// <summary>
        /// Canvas height (in pixels).
        /// </summary>
        public int Height { get; private set; } = default!;

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; private set; } = default!;

        // Methods.
        public async Task<IEnumerable<ThumbnailSourceFile>> GetScaledThumbnailsAsync(
            DirectoryInfo importerTempDirectoryInfo)
        {
            ArgumentNullException.ThrowIfNull(importerTempDirectoryInfo, nameof(importerTempDirectoryInfo));

            List<ThumbnailSourceFile> thumbnails = new();

            using var thumbFileStream = (await ReadToStreamAsync()).Stream;
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

                thumbnails.Add(await BuildNewAsync(
                    new SourceUri(thumbnailResizedPath, SourceUriKind.Local),
                    HttpClientFactory));
            }

            return thumbnails;
        }
    }
}
