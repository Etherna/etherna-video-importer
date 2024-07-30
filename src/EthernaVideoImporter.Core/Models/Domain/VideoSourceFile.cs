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

using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class VideoSourceFile : SourceFile, IVideoFile
    {
        // Constructor.
        private VideoSourceFile(
            UniversalFile universalFile,
            VideoSourceType videoType)
            : base(universalFile)
        {
            VideoType = videoType;
        }

        // Static builders.
        public static async Task<VideoSourceFile> BuildNewAsync(
            UniversalFile universalFile,
            VideoSourceType videoType,
            IFFmpegService ffMpegService)
        {
            ArgumentNullException.ThrowIfNull(ffMpegService, nameof(ffMpegService));
            ArgumentNullException.ThrowIfNull(universalFile, nameof(universalFile));

            var video = new VideoSourceFile(universalFile, videoType);

            var (absoluteFileUri, _) = universalFile.FileUri.ToAbsoluteUri();
            var ffProbeResult = await ffMpegService.GetVideoInfoAsync(absoluteFileUri);

            video.Duration = ffProbeResult.Format.Duration;
            video.Height = ffProbeResult.Streams.First().Height;
            video.Width = ffProbeResult.Streams.First().Width;

            return video;
        }

        // Properties.
        public TimeSpan Duration { get; private set; }
        public int Height { get; private set; }
        public string QualityLabel => $"{Height}p";
        public VideoSourceType VideoType { get; }
        public int Width { get; private set; }
    }
}
