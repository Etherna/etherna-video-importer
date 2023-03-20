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

using Blurhash.SkiaSharp;
using Etherna.VideoImporter.Core.Models.Domain;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestThumbnailDto
    {
        // Constructors.
        public ManifestThumbnailDto(IEnumerable<ThumbnailLocalFile> thumbnailFiles)
        {
            if (thumbnailFiles is null)
                throw new ArgumentNullException(nameof(thumbnailFiles));

            var thumbnailFilesList = thumbnailFiles.ToList();
            if (!thumbnailFilesList.Any())
                throw new ArgumentException($"Empty {nameof(thumbnailFiles)}");

            Sources = thumbnailFilesList.ToDictionary(t => t.Width.ToString(CultureInfo.InvariantCulture), t => t.UploadedHashReference!);
           
            var thumbnailFile = thumbnailFilesList.First();
            using var thumbFileStream = File.OpenRead(thumbnailFile.DownloadedFilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
            AspectRatio = (float)thumbnailFile.Width / thumbnailFile.Height;
            Blurhash = Blurhasher.Encode(thumbBitmap, 4, 4);
        }

        public ManifestThumbnailDto(
            float aspectRatio,
            string blurhash,
            IDictionary<string, string> sources)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Sources = sources;
        }

        // Properties.
        public float AspectRatio { get; }
        public string Blurhash { get; }
        public IDictionary<string, string> Sources { get; }
    }
}
