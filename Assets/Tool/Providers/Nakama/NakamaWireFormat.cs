#if NAKAMA
using System.Collections.Generic;
using PurrNet.Packing;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Match-state op codes for Nakama lobby messages. Payloads are BitPacker-encoded.</summary>
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

    internal struct SnapshotMessage : IPackedAuto
    {
        public string hostUserId;
        public string lobbyName;
        public string code;
        public int maxPlayers;
        public bool joinable;
        public Dictionary<string, string> metadata;
        public Dictionary<string, Dictionary<string, string>> playerMetadata;
        public Dictionary<string, string> displayNames;
    }

    internal struct LobbyMetadataMessage : IPackedAuto
    {
        public Dictionary<string, string> metadata;
    }

    internal struct PlayerMetadataMessage : IPackedAuto
    {
        public string userId;
        public Dictionary<string, string> metadata;
    }

    internal struct KickMessage : IPackedAuto
    {
        public string userId;
    }

    internal struct JoinableMessage : IPackedAuto
    {
        public bool joinable;
    }

    internal struct HostMigrationMessage : IPackedAuto
    {
        public string hostUserId;
    }
}
#endif
