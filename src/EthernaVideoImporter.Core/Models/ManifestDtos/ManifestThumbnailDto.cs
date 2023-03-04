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
using Etherna.VideoImporter.Core.Models.Domain;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Etherna.VideoImporter.Core.Models.ManifestDtos
{
    public sealed class ManifestThumbnailDto
    {
        // Constructors.
        public ManifestThumbnailDto(ThumbnailFile thumbnailFile)
        {
            if (thumbnailFile?.UploadedHashReference is null)
                throw new ArgumentNullException(nameof(thumbnailFile));

            using var thumbFileStream = File.OpenRead(thumbnailFile.DownloadedFilePath);
            using var thumbManagedStream = new SKManagedStream(thumbFileStream);
            using var thumbBitmap = SKBitmap.Decode(thumbManagedStream);

            AspectRatio = thumbnailFile.Width / thumbnailFile.Height;
            Blurhash = Blurhasher.Encode(thumbBitmap, 4, 4);
            Sources = new Dictionary<string, string>() { { $"{thumbBitmap.Width}w", thumbnailFile.UploadedHashReference } };
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