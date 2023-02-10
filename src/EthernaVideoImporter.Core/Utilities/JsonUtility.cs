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
