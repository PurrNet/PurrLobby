using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PurrNet.Services
{
    [Serializable]
    public struct WsMessage
    {
        [JsonProperty("type")]
        public string type;
    }

    [Serializable]
    public struct WsAuthMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("apiKey", NullValueHandling = NullValueHandling.Ignore)]
        public string apiKey;

        [JsonProperty("sessionToken")]
        public string sessionToken;

        [JsonProperty("playerToken")]
        public string playerToken;
    }

    [Serializable]
    public struct WsPingMessage
    {
        [JsonProperty("type")]
        public string type;
    }

    [Serializable]
    public struct WsAuthenticatedMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("snapshot")]
        public LobbySnapshot snapshot;
    }

    [Serializable]
    public struct WsSnapshotMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("snapshot")]
        public LobbySnapshot snapshot;
    }

    [Serializable]
    public struct WsChatMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("message")]
        public ChatMessage message;
    }

    [Serializable]
    public struct WsPlayerJoinedMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("player")]
        public LobbyPlayer player;

        [JsonProperty("version")]
        public int version;
    }

    [Serializable]
    public struct WsPlayerLeftMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("playerId")]
        public string playerId;

        [JsonProperty("newHostPlayerId")]
        public string newHostPlayerId;

        [JsonProperty("version")]
        public int version;
    }

    [Serializable]
    public struct WsJoinableChangedMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("joinable")]
        public bool joinable;

        [JsonProperty("version")]
        public int version;
    }

    [Serializable]
    public struct WsMetadataUpdatedMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("metadata")]
        public Dictionary<string, string> metadata;

        [JsonProperty("version")]
        public int version;
    }

    [Serializable]
    public struct WsPlayerMetadataUpdatedMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("playerId")]
        public string playerId;

        [JsonProperty("metadata")]
        public Dictionary<string, string> metadata;

        [JsonProperty("version")]
        public int version;
    }

    [Serializable]
    public struct WsErrorMessage
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("message")]
        public string message;
    }

    public static class WsMessageParser
    {
        public static string GetMessageType(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                return obj["type"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static T Parse<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static WsAuthenticatedMessage ParseAuthenticated(string json)
        {
            return JsonConvert.DeserializeObject<WsAuthenticatedMessage>(json);
        }

        public static WsSnapshotMessage ParseSnapshot(string json)
        {
            return JsonConvert.DeserializeObject<WsSnapshotMessage>(json);
        }

        public static WsChatMessage ParseChat(string json)
        {
            return JsonConvert.DeserializeObject<WsChatMessage>(json);
        }

        public static WsErrorMessage ParseError(string json)
        {
            return JsonConvert.DeserializeObject<WsErrorMessage>(json);
        }
    }
}
