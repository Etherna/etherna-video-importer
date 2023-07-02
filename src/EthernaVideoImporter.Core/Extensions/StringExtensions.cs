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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace Etherna.VideoImporter.Core.Extensions
{
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Better keep strings")]
    public static class StringExtensions
    {
        public static UriType GetUriType(this string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            if (path.Intersect(Path.GetInvalidPathChars()).Any())
                throw new ArgumentException("Path contains invalid chars", nameof(path));

            // Check if it's a URL.
            if (Uri.TryCreate(path, UriKind.Absolute, out var _))
                return UriType.Url;

            // Check if it's an absolute path.
            if (Path.IsPathRooted(path))
                return UriType.Absolute;

            // If it's not a URL or an absolute path, it's a relative path.
            return UriType.Relative;
        }

        public static string ToAbsoluteUri(this string path) =>
            ToAbsoluteUri(path, Directory.GetCurrentDirectory());

        public static string ToAbsoluteUri(this string path, string currentDirectory) =>
            path.GetUriType() switch
            {
                UriType.Absolute => path,
                UriType.Relative => Path.GetFullPath(path, currentDirectory),
                UriType.Url => path,
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
