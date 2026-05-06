#if NAKAMA
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Match-state op codes for Nakama lobby messages. Payloads are UTF-8 JSON.</summary>
    internal static class NakamaOpCodes
    {
        public const long Snapshot = 1;
        public const long LobbyMetadataPatch = 2;
        public const long PlayerMetadataPatch = 3;
        public const long Chat = 4;
        public const long Kick = 5;
        public const long SetJoinable = 6;
        public const long HostMigration = 7;
        public const long RequestSnapshot = 8;
    }

    /// <summary>Full lobby snapshot sent by the host on join and after host migration.</summary>
    internal class SnapshotMessage
    {
        [JsonProperty("hostUserId")] public string hostUserId;
        [JsonProperty("lobbyName")] public string lobbyName;
        [JsonProperty("code")] public string code;
        [JsonProperty("maxPlayers")] public int maxPlayers;
        [JsonProperty("joinable")] public bool joinable;
        [JsonProperty("metadata")] public Dictionary<string, string> metadata;
        [JsonProperty("playerMetadata")] public Dictionary<string, Dictionary<string, string>> playerMetadata;
        [JsonProperty("displayNames")] public Dictionary<string, string> displayNames;
    }

    internal class LobbyMetadataMessage
    {
        [JsonProperty("metadata")] public Dictionary<string, string> metadata;
    }

    internal class PlayerMetadataMessage
    {
        [JsonProperty("userId")] public string userId;
        [JsonProperty("metadata")] public Dictionary<string, string> metadata;
    }

    internal class KickMessage
    {
        [JsonProperty("userId")] public string userId;
    }

    internal class JoinableMessage
    {
        [JsonProperty("joinable")] public bool joinable;
    }

    internal class HostMigrationMessage
    {
        [JsonProperty("hostUserId")] public string hostUserId;
    }
}
#endif
