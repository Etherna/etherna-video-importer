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
using System.IO;
using System.Linq;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestThumbnailDto
    {
        // Constructors.
        public ManifestThumbnailDto(IEnumerable<ThumbnailFile> thumbnailFiles)
        {
            if (thumbnailFiles is null)
                throw new ArgumentNullException(nameof(thumbnailFiles));

            Sources = new Dictionary<string, string>();
            foreach (var thumbnailFile in thumbnailFiles)
            {
                if (string.IsNullOrWhiteSpace(thumbnailFile.UploadedHashReference))
                    throw new ArgumentException("Empty data in UploadedHashReference");

                Sources.Add($"{thumbnailFile.Width}w", thumbnailFile.UploadedHashReference);

                // Save the best aspect ratio.
                var currentAspectRatio = thumbnailFile.Width / thumbnailFile.Height;
                if (currentAspectRatio > AspectRatio)
                {
                    using var thumbFileStream = File.OpenRead(thumbnailFile.DownloadedFilePath);
                    using var thumbManagedStream = new SKManagedStream(thumbFileStream);
                    using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);
                    AspectRatio = currentAspectRatio;
                    Blurhash = Blurhasher.Encode(thumbBitmap, 4, 4);
                }
            }

            if (string.IsNullOrWhiteSpace(Blurhash))
                throw new ArgumentException("Empty data in Blurhash");
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
        public float AspectRatio { get; set; }
        public string Blurhash { get; set; }
        public IDictionary<string, string> Sources { get; }
    }
}
