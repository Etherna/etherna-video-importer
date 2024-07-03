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

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Etherna.VideoImporter.Core
{
    public sealed class CommonConsts
    {
        public const int BeePort = 1633;
        public const string BeeNodeUrl = "http://localhost/";
        public static readonly string DefaultFFmpegFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFmpeg");
        public const int DownloadMaxRetry = 3;
        public static readonly TimeSpan DownloadTimespanRetry = TimeSpan.FromMilliseconds(3500);
        public const string EthernaVideoImporterClientId = "ethernaVideoImporterId";
        public static string FFmpegBinaryName
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "ffmpeg.exe";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "ffmpeg";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "ffmpeg";

                throw new InvalidOperationException("OS not supported");
            }
        }
        public static string FFprobeBinaryName
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "ffprobe.exe";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "ffprobe";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "ffprobe";

                throw new InvalidOperationException("OS not supported");
            }
        }
        public const string ImporterIdentifier = "EthernaImporter";
        public static DirectoryInfo TempDirectory { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), ImporterIdentifier));
    }
}
