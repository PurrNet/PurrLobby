#if NAKAMA
using System.Collections.Generic;
using System.Text;
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

    internal interface INakamaPayload
    {
        void Write(BitPacker packer);
    }

    internal struct SnapshotMessage : INakamaPayload
    {
        public string hostUserId;
        public string lobbyName;
        public string code;
        public int maxPlayers;
        public bool joinable;
        public Dictionary<string, string> metadata;
        public Dictionary<string, Dictionary<string, string>> playerMetadata;
        public Dictionary<string, string> displayNames;

        public void Write(BitPacker packer)
        {
            NakamaWire.WriteStr(packer, hostUserId);
            NakamaWire.WriteStr(packer, lobbyName);
            NakamaWire.WriteStr(packer, code);
            Packer<int>.Write(packer, maxPlayers);
            Packer<bool>.Write(packer, joinable);
            NakamaWire.WriteDict(packer, metadata);
            NakamaWire.WriteNestedDict(packer, playerMetadata);
            NakamaWire.WriteDict(packer, displayNames);
        }

        public static SnapshotMessage Read(BitPacker packer) => new SnapshotMessage
        {
            hostUserId = NakamaWire.ReadStr(packer),
            lobbyName = NakamaWire.ReadStr(packer),
            code = NakamaWire.ReadStr(packer),
            maxPlayers = Packer<int>.Read(packer),
            joinable = Packer<bool>.Read(packer),
            metadata = NakamaWire.ReadDict(packer),
            playerMetadata = NakamaWire.ReadNestedDict(packer),
            displayNames = NakamaWire.ReadDict(packer),
        };
    }

    internal struct LobbyMetadataMessage : INakamaPayload
    {
        public Dictionary<string, string> metadata;

        public void Write(BitPacker packer) => NakamaWire.WriteDict(packer, metadata);

        public static LobbyMetadataMessage Read(BitPacker packer) => new LobbyMetadataMessage
        {
            metadata = NakamaWire.ReadDict(packer),
        };
    }

    internal struct PlayerMetadataMessage : INakamaPayload
    {
        public string userId;
        public Dictionary<string, string> metadata;

        public void Write(BitPacker packer)
        {
            NakamaWire.WriteStr(packer, userId);
            NakamaWire.WriteDict(packer, metadata);
        }

        public static PlayerMetadataMessage Read(BitPacker packer) => new PlayerMetadataMessage
        {
            userId = NakamaWire.ReadStr(packer),
            metadata = NakamaWire.ReadDict(packer),
        };
    }

    internal struct KickMessage : INakamaPayload
    {
        public string userId;

        public void Write(BitPacker packer) => NakamaWire.WriteStr(packer, userId);

        public static KickMessage Read(BitPacker packer) => new KickMessage
        {
            userId = NakamaWire.ReadStr(packer),
        };
    }

    internal struct JoinableMessage : INakamaPayload
    {
        public bool joinable;

        public void Write(BitPacker packer) => Packer<bool>.Write(packer, joinable);

        public static JoinableMessage Read(BitPacker packer) => new JoinableMessage
        {
            joinable = Packer<bool>.Read(packer),
        };
    }

    internal struct HostMigrationMessage : INakamaPayload
    {
        public string hostUserId;

        public void Write(BitPacker packer) => NakamaWire.WriteStr(packer, hostUserId);

        public static HostMigrationMessage Read(BitPacker packer) => new HostMigrationMessage
        {
            hostUserId = NakamaWire.ReadStr(packer),
        };
    }

    internal static class NakamaWire
    {
        public static void WriteStr(BitPacker packer, string value)
        {
            bool hasValue = value != null;
            Packer<bool>.Write(packer, hasValue);
            if (hasValue)
                packer.WriteString(Encoding.UTF8, value);
        }

        public static string ReadStr(BitPacker packer)
        {
            return Packer<bool>.Read(packer) ? packer.ReadString(Encoding.UTF8) : null;
        }

        public static void WriteDict(BitPacker packer, Dictionary<string, string> dict)
        {
            bool hasValue = dict != null;
            Packer<bool>.Write(packer, hasValue);
            if (!hasValue)
                return;

            Packer<int>.Write(packer, dict.Count);
            foreach (var kvp in dict)
            {
                WriteStr(packer, kvp.Key);
                WriteStr(packer, kvp.Value); // null value is a delete marker in patches
            }
        }

        public static Dictionary<string, string> ReadDict(BitPacker packer)
        {
            if (!Packer<bool>.Read(packer))
                return null;

            int count = Packer<int>.Read(packer);
            var dict = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                var key = ReadStr(packer);
                var value = ReadStr(packer);
                if (key != null)
                    dict[key] = value;
            }
            return dict;
        }

        public static void WriteNestedDict(BitPacker packer, Dictionary<string, Dictionary<string, string>> dict)
        {
            bool hasValue = dict != null;
            Packer<bool>.Write(packer, hasValue);
            if (!hasValue)
                return;

            Packer<int>.Write(packer, dict.Count);
            foreach (var kvp in dict)
            {
                WriteStr(packer, kvp.Key);
                WriteDict(packer, kvp.Value);
            }
        }

        public static Dictionary<string, Dictionary<string, string>> ReadNestedDict(BitPacker packer)
        {
            if (!Packer<bool>.Read(packer))
                return null;

            int count = Packer<int>.Read(packer);
            var dict = new Dictionary<string, Dictionary<string, string>>(count);
            for (int i = 0; i < count; i++)
            {
                var key = ReadStr(packer);
                var value = ReadDict(packer);
                if (key != null)
                    dict[key] = value;
            }
            return dict;
        }
    }
}
#endif
