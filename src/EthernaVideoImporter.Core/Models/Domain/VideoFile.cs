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

using Etherna.BeeNet.Models;
using Etherna.Sdk.Users.Index.Models;
using Etherna.UniversalFiles;
using Etherna.VideoImporter.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed class VideoFile : FileBase
    {
        // Constructor.
        private VideoFile(
            long byteSize,
            string fileName,
            TimeSpan duration,
            int height,
            int width,
            UFile universalFile,
            VideoType videoType,
            SwarmHash? swarmHash)
            : base(byteSize, fileName, universalFile, swarmHash)
        {
            Duration = duration;
            Height = height;
            VideoType = videoType;
            Width = width;
        }

        // Static builders.
        public static async Task<VideoFile> BuildNewAsync(
            IFFmpegService ffMpegService,
            BasicUFile uFile,
            SwarmHash? swarmHash = null)
        {
            ArgumentNullException.ThrowIfNull(ffMpegService, nameof(ffMpegService));
            ArgumentNullException.ThrowIfNull(uFile, nameof(uFile));

            var byteSize = await uFile.GetByteSizeAsync();
            var fileName = await uFile.TryGetFileNameAsync() ??
                           throw new InvalidOperationException($"Can't get file name from {uFile.FileUri.OriginalUri}");

            var (absoluteFileUri, _) = uFile.FileUri.ToAbsoluteUri();
            var ffProbeResult = await ffMpegService.GetVideoInfoAsync(absoluteFileUri);
            var videoType = Path.GetExtension(fileName) switch
            {
                ".mp4" => VideoType.Mp4,
                ".m3u8" => VideoType.Hls,
                ".mpd" => VideoType.Dash,
                _ => VideoType.Unknown
            };

            return new VideoFile(
                byteSize,
                fileName,
                ffProbeResult.Format.Duration,
                ffProbeResult.Streams.First().Height,
                ffProbeResult.Streams.First().Width,
                uFile,
                videoType,
                swarmHash);
        }

        // Properties.
        public TimeSpan Duration { get; private set; }
        public int Height { get; }
        public string QualityLabel => $"{Height}p";
        public VideoType VideoType { get; }
        public int Width { get; }
    }
}
