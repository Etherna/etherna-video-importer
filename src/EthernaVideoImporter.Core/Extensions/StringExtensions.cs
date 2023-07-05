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

using Etherna.VideoImporter.Core.Models.Domain;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Etherna.VideoImporter.Core.Extensions
{
    public static class StringExtensions
    {
        public static PathType GetPathType(this string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            if (path.Intersect(Path.GetInvalidPathChars()).Any())
                throw new ArgumentException("Path contains invalid chars", nameof(path));

            // Check if it's a URL.
            if (Uri.TryCreate(path, UriKind.Absolute, out var _))
                return PathType.Url;

            // Check if it's an absolute path.
            if (Path.IsPathRooted(path))
                return PathType.Absolute;

            // If it's not a URL or an absolute path, it's a relative path.
            return PathType.Relative;
        }

        public static string ToAbsolutePath(this string path, string currentDirectory) =>
            path.GetPathType() switch
            {
                PathType.Relative => Path.Combine(currentDirectory, path),
                PathType.Absolute => path,
                PathType.Url => path,
                _ => throw new InvalidOperationException()
            };

        public static string ToSafeFileName(this string value)
        {
            var strBuilder = new StringBuilder(value);
            foreach (char c in Path.GetInvalidFileNameChars())
                strBuilder = strBuilder.Replace(c, '_');

            return strBuilder.ToString();
        }
    }
}
