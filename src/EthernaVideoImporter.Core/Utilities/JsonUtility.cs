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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etherna.VideoImporter.Core.Utilities
{
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions serializeOptions = new()
        {
            Converters = {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        public static string ToJson<T>(T objectToSerialize) where T : class
        {
            return JsonSerializer.Serialize(objectToSerialize, serializeOptions);
        }

        public static T? FromJson<T>(this string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, serializeOptions);
        }

    }
}
