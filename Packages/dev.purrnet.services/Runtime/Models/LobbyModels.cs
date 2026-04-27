using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PurrNet.Services
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LobbyVisibility
    {
        [EnumMember(Value = "public")]
        Public,
        [EnumMember(Value = "private")]
        Private
    }

    [Serializable]
    public struct LobbyData
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("ownerId")]
        public string ownerId;

        [JsonProperty("hostPlayerId")]
        public string hostPlayerId;

        [JsonProperty("maxPlayers")]
        public int maxPlayers;

        [JsonProperty("visibility")]
        public LobbyVisibility visibility;

        [JsonProperty("joinable")]
        public bool joinable;

        [JsonProperty("code")]
        public string code;

        [JsonProperty("chatSeq")]
        public int chatSeq;

        [JsonProperty("version")]
        public int version;

        [JsonProperty("createdAt")]
        public long createdAt;
    }

    [Serializable]
    public struct LobbyPlayer
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("displayName")]
        public string displayName;

        [JsonProperty("joinedAt")]
        public long joinedAt;

        [JsonProperty("lastSeen")]
        public long lastSeen;
    }

    [Serializable]
    public struct ChatMessage
    {
        [JsonProperty("seq")]
        public int seq;

        [JsonProperty("playerId")]
        public string playerId;

        [JsonProperty("playerName")]
        public string playerName;

        [JsonProperty("data")]
        public string data;

        [JsonProperty("timestamp")]
        public long timestamp;
    }

    [Serializable]
    public struct LobbySnapshot
    {
        [JsonProperty("lobby")]
        public LobbyData lobby;

        [JsonProperty("players")]
        public List<LobbyPlayer> players;

        [JsonProperty("metadata")]
        public Dictionary<string, string> metadata;

        [JsonProperty("playerMetadata")]
        public Dictionary<string, Dictionary<string, string>> playerMetadata;

        [JsonProperty("chat")]
        public List<ChatMessage> chat;
    }

    [Serializable]
    public struct CreateLobbyRequest
    {
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string name;

        [JsonProperty("maxPlayers", NullValueHandling = NullValueHandling.Ignore)]
        public int? maxPlayers;

        [JsonProperty("visibility", NullValueHandling = NullValueHandling.Ignore)]
        public LobbyVisibility? visibility;

        [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> metadata;
    }

    public struct CreateLobbyOptions
    {
        public string name;
        public int maxPlayers;
        public LobbyVisibility visibility;
        public Dictionary<string, string> metadata;

        public static CreateLobbyOptions Default => new CreateLobbyOptions
        {
            maxPlayers = 8,
            visibility = LobbyVisibility.Private
        };
    }

    [Serializable]
    public struct CreateLobbyResponse
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("ownerId")]
        public string ownerId;

        [JsonProperty("hostPlayerId")]
        public string hostPlayerId;

        [JsonProperty("maxPlayers")]
        public int maxPlayers;

        [JsonProperty("visibility")]
        public LobbyVisibility visibility;

        [JsonProperty("joinable")]
        public bool joinable;

        [JsonProperty("code")]
        public string code;

        [JsonProperty("chatSeq")]
        public int chatSeq;

        [JsonProperty("version")]
        public int version;

        [JsonProperty("createdAt")]
        public long createdAt;

        [JsonProperty("playerToken")]
        public string playerToken;
    }

    [Serializable]
    public struct JoinByCodeRequest
    {
        [JsonProperty("code")]
        public string code;
    }

    [Serializable]
    public struct JoinResponse
    {
        [JsonProperty("lobbyId")]
        public string lobbyId;

        [JsonProperty("playerToken")]
        public string playerToken;

        [JsonProperty("success")]
        public bool success;
    }

    [Serializable]
    public struct QuickJoinRequest
    {
        [JsonProperty("filter", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> filter;
    }

    [Serializable]
    public struct QuickJoinQueryRequest
    {
        [JsonProperty("stringFilters", NullValueHandling = NullValueHandling.Ignore)]
        public List<StringFilterEntry> stringFilters;

        [JsonProperty("numericalFilters", NullValueHandling = NullValueHandling.Ignore)]
        public List<NumericalFilterEntry> numericalFilters;

        [JsonProperty("slotsAvailable", NullValueHandling = NullValueHandling.Ignore)]
        public int? slotsAvailable;
    }

    [Serializable]
    public struct StringFilterEntry
    {
        [JsonProperty("key")] public string key;
        [JsonProperty("op")] public string op;
        [JsonProperty("value")] public string value;
    }

    [Serializable]
    public struct NumericalFilterEntry
    {
        [JsonProperty("key")] public string key;
        [JsonProperty("op")] public string op;
        [JsonProperty("value")] public int value;
    }

    [Serializable]
    public struct SetJoinableRequest
    {
        [JsonProperty("joinable")]
        public bool joinable;
    }

    [Serializable]
    public struct KickRequest
    {
        [JsonProperty("playerId")]
        public string playerId;
    }

    [Serializable]
    public struct MetadataRequest
    {
        [JsonProperty("metadata")]
        public Dictionary<string, string> metadata;
    }

    [Serializable]
    public struct ChatRequest
    {
        [JsonProperty("data")]
        public string data;
    }

    [Serializable]
    public struct ChatSendResponse
    {
        [JsonProperty("seq")]
        public int seq;
    }

    [Serializable]
    public struct LobbyListEntry
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("ownerId")]
        public string ownerId;

        [JsonProperty("hostPlayerId")]
        public string hostPlayerId;

        [JsonProperty("maxPlayers")]
        public int maxPlayers;

        [JsonProperty("visibility")]
        public LobbyVisibility visibility;

        [JsonProperty("joinable")]
        public bool joinable;

        [JsonProperty("code")]
        public string code;

        [JsonProperty("chatSeq")]
        public int chatSeq;

        [JsonProperty("version")]
        public int version;

        [JsonProperty("createdAt")]
        public long createdAt;

        [JsonProperty("playerCount")]
        public int playerCount;

        [JsonProperty("metadata")]
        public Dictionary<string, string> metadata;
    }

    [Serializable]
    public struct LobbyListResponse
    {
        [JsonProperty("lobbies")]
        public List<LobbyListEntry> lobbies;
    }

    [Serializable]
    public struct SuccessResponse
    {
        [JsonProperty("success")]
        public bool success;

        [JsonProperty("destroyed")]
        public bool destroyed;
    }

    public struct ServiceResult
    {
        public bool success;
        public string error;
    }

    public struct LobbyResult
    {
        public bool success;
        public LobbyData lobby;
        public string playerToken;
        public string error;
    }

    public struct JoinResult
    {
        public bool success;
        public string lobbyId;
        public string playerToken;
        public string error;
    }

    public struct LobbyListResult
    {
        public bool success;
        public List<LobbyListEntry> lobbies;
        public string error;
    }

    public struct LobbySnapshotResult
    {
        public bool success;
        public LobbySnapshot snapshot;
        public string error;
    }

    public struct ChatResult
    {
        public bool success;
        public int seq;
        public string error;
    }

    public enum LobbyComparison
    {
        Equal,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual
    }

    public class LobbyQuery
    {
        readonly List<(string key, LobbyComparison op, string value)> _stringFilters = new();
        readonly List<(string key, LobbyComparison op, int value)> _numericalFilters = new();
        readonly List<(string key, int target)> _nearSorts = new();
        int? _slotsAvailable;
        int? _maxResults;

        public LobbyQuery AddStringFilter(string key, LobbyComparison op, string value)
        {
            _stringFilters.Add((key, op, value));
            return this;
        }

        public LobbyQuery AddNumericalFilter(string key, LobbyComparison op, int value)
        {
            _numericalFilters.Add((key, op, value));
            return this;
        }

        public LobbyQuery AddNearValueFilter(string key, int target)
        {
            _nearSorts.Add((key, target));
            return this;
        }

        public LobbyQuery SetSlotsAvailable(int slots)
        {
            _slotsAvailable = slots;
            return this;
        }

        public LobbyQuery SetMaxResults(int max)
        {
            _maxResults = max;
            return this;
        }

        static string OpToString(LobbyComparison op)
        {
            switch (op)
            {
                case LobbyComparison.Equal: return "eq";
                case LobbyComparison.NotEqual: return "neq";
                case LobbyComparison.LessThan: return "lt";
                case LobbyComparison.GreaterThan: return "gt";
                case LobbyComparison.LessThanOrEqual: return "lte";
                case LobbyComparison.GreaterThanOrEqual: return "gte";
                default: return "eq";
            }
        }

        internal string ToQueryString()
        {
            var sb = new System.Text.StringBuilder();

            foreach (var (key, op, value) in _stringFilters)
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append("filter.");
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(OpToString(op) + ":" + value));
            }

            foreach (var (key, op, value) in _numericalFilters)
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append("nfilter.");
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(OpToString(op) + ":" + value));
            }

            foreach (var (key, target) in _nearSorts)
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append("near.");
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(target);
            }

            if (_slotsAvailable.HasValue)
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append("slotsAvailable=");
                sb.Append(_slotsAvailable.Value);
            }

            if (_maxResults.HasValue)
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append("maxResults=");
                sb.Append(_maxResults.Value);
            }

            return sb.ToString();
        }

        internal QuickJoinQueryRequest ToQuickJoinBody()
        {
            var req = new QuickJoinQueryRequest();

            if (_stringFilters.Count > 0)
            {
                req.stringFilters = new List<StringFilterEntry>();
                foreach (var (key, op, value) in _stringFilters)
                    req.stringFilters.Add(new StringFilterEntry { key = key, op = OpToString(op), value = value });
            }

            if (_numericalFilters.Count > 0)
            {
                req.numericalFilters = new List<NumericalFilterEntry>();
                foreach (var (key, op, value) in _numericalFilters)
                    req.numericalFilters.Add(new NumericalFilterEntry { key = key, op = OpToString(op), value = value });
            }

            if (_slotsAvailable.HasValue)
                req.slotsAvailable = _slotsAvailable.Value;

            return req;
        }
    }
}
