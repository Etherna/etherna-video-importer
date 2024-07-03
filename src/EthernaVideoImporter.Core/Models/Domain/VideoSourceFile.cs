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

using Etherna.VideoImporter.Core.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class VideoSourceFile : SourceFile, IVideoFile
    {
        // Constructor.
        private VideoSourceFile(
            SourceUri fileUri,
            IHttpClientFactory httpClientFactory)
            : base(fileUri, httpClientFactory)
        { }

        // Static builders.
        public static async Task<VideoSourceFile> BuildNewAsync(
            SourceUri fileUri,
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(fileUri, nameof(fileUri));
            ArgumentNullException.ThrowIfNull(ffMpegService, nameof(ffMpegService));

            var video = new VideoSourceFile(fileUri, httpClientFactory);

            var (absoluteFileUri, _) = fileUri.ToAbsoluteUri();
            var ffProbeResult = await ffMpegService.GetVideoInfoAsync(absoluteFileUri);

            video.Duration = ffProbeResult.Format.Duration;
            video.Height = ffProbeResult.Streams.First().Height;
            video.Width = ffProbeResult.Streams.First().Width;

            return video;
        }

        // Properties.
        public TimeSpan Duration { get; private set; }
        public int Height { get; private set; }
        public string VideoQualityLabel => $"{Height}p";
        public int Width { get; private set; }
    }
}
