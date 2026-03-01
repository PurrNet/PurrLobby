using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PurrLobby.Internal
{
    internal static class Json
    {
        public static JObject ParseObject(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JObject.Parse(json);
        }

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }

    internal static class JsonExtensions
    {
        public static string GetString(this JObject obj, string key, string fallback = null)
        {
            if (obj == null) return fallback;
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return (string)token;
        }

        public static int GetInt(this JObject obj, string key, int fallback = 0)
        {
            if (obj == null) return fallback;
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return (int)token;
        }

        public static long GetLong(this JObject obj, string key, long fallback = 0)
        {
            if (obj == null) return fallback;
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return (long)token;
        }

        public static JObject GetObject(this JObject obj, string key)
        {
            return obj?[key] as JObject;
        }

        public static JArray GetArray(this JObject obj, string key)
        {
            return obj?[key] as JArray;
        }

        public static Dictionary<string, string> GetStringDict(this JObject obj, string key)
        {
            var child = obj.GetObject(key);
            if (child == null) return new Dictionary<string, string>();
            var result = new Dictionary<string, string>();
            foreach (var kv in child)
                result[kv.Key] = kv.Value?.ToString() ?? string.Empty;
            return result;
        }
    }
}
