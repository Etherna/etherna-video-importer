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

using Etherna.VideoImporter.Core.Services;
using System;
using System.Linq;
using System.Net.Http;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public sealed partial class VideoSourceFile : SourceFile, IVideoFile
    {
        // Constructor.
        private VideoSourceFile(
            SourceUri fileUri,
            IHttpClientFactory httpClientFactory)
            : base(fileUri, httpClientFactory)
        { }

        // Static builders.
        public static VideoSourceFile BuildNew(
            SourceUri fileUri,
            IFFmpegService ffMpegService,
            IHttpClientFactory httpClientFactory)
        {
            if (fileUri is null)
                throw new ArgumentNullException(nameof(fileUri));
            if (ffMpegService is null)
                throw new ArgumentNullException(nameof(ffMpegService));

            var video = new VideoSourceFile(fileUri, httpClientFactory);

            var (absoluteFileUri, _) = fileUri.ToAbsoluteUri();
            var ffProbeResult = ffMpegService.GetVideoInfo(absoluteFileUri);

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
