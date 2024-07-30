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
using Etherna.UniversalFiles;
using SkiaSharp;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class ThumbnailSourceFile : SourceFile, IThumbnailFile
    {
        // Constructor.
        private ThumbnailSourceFile(UniversalFile universalFile)
            : base(universalFile)
        { }

        // Static builders.
        public static async Task<ThumbnailSourceFile> BuildNewAsync(
            UniversalFile universalFile)
        {
            var thumbnail = new ThumbnailSourceFile(universalFile);

            using var thumbFileStream = await thumbnail.ReadToStreamAsync();
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
        public int Height { get; private set; }

        /// <summary>
        /// Canvas width (in pixels).
        /// </summary>
        public int Width { get; private set; }
    }
}
