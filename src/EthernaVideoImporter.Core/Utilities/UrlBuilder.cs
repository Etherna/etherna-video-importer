// Copyright 2022-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics.CodeAnalysis;

namespace Etherna.VideoImporter.Core.Utilities
{
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings")]
    public static class UrlBuilder
    {
        // Consts.
        public const string EmbeddedUrlPrefix = "https://etherna.io/embed/";
        public const string NormalUrlPrefix = "https://etherna.io/watch/";
        
        // Static methods.
        public static string BuildEmbeddedIndexUrl(string indexId) => EmbeddedUrlPrefix + indexId;
        public static string BuildEmbeddedPermalinkUrl(string referenceHash) => EmbeddedUrlPrefix + referenceHash;
        public static string BuildNormalIndexUrl(string indexId) => NormalUrlPrefix + indexId;
        public static string BuildNormalPermalinkUrl(string referenceHash) => NormalUrlPrefix + referenceHash;
    }
}