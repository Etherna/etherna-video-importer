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
using Etherna.BeeNet.Models;
using Etherna.Sdk.Tools.Video.Models;
using Etherna.UniversalFiles;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class ThumbnailFile : FileBase
    {
        // Constructor.
        private ThumbnailFile(
            string blurhash,
            long byteSize,
            string fileName,
            ImageType imageType,
            int height,
            int width,
            UFile universalFile,
            SwarmHash? swarmHash)
            : base(byteSize, fileName, universalFile, swarmHash)
        {
            Blurhash = blurhash;
            Height = height;
            ImageType = imageType;
            Width = width;
        }

        // Static builders.
        public static async Task<ThumbnailFile> BuildNewAsync(
            BasicUFile uFile,
            SwarmHash? swarmHash = null)
        {
            ArgumentNullException.ThrowIfNull(uFile, nameof(uFile));

            // Get image info.
            var byteSize = await uFile.GetByteSizeAsync();
            var fileName = await uFile.TryGetFileNameAsync() ??
                           throw new InvalidOperationException($"Can't get file name from {uFile.FileUri.OriginalUri}");

            var (thumbByteArray, _) = await uFile.ReadToByteArrayAsync();
            
            using var thumbStream = new MemoryStream(thumbByteArray);
            using var thumbManagedStream = new SKManagedStream(thumbStream);
            using var codec = SKCodec.Create(thumbManagedStream);

            var imageType = codec.EncodedFormat switch
            {
                SKEncodedImageFormat.Jpeg => ImageType.Jpeg,
                SKEncodedImageFormat.Png => ImageType.Png,
                SKEncodedImageFormat.Webp => ImageType.Webp,
                SKEncodedImageFormat.Avif => ImageType.Avif,
                _ => ImageType.Unknown
            };

            using var thumbBitmap = SKBitmap.Decode(thumbByteArray);
            return new ThumbnailFile(
                Blurhasher.Encode(thumbBitmap, 4, 4),
                byteSize,
                fileName,
                imageType,
                thumbBitmap.Height,
                thumbBitmap.Width,
                uFile,
                swarmHash);
        }

        // Properties.
        public float AspectRatio => (float)Width / Height;

        public string Blurhash { get; }

        /// <summary>
        /// Canvas height (in pixels).
        /// </summary>
        public int Height { get; }

        public ImageType ImageType { get; }

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; }
    }
}
