#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Metadata backed by Steam lobby data. Lobby metadata maps to
    /// SetLobbyData/DeleteLobbyData (owner-only). Player metadata is a single
    /// JSON blob in member data (<see cref="SteamLobby.MemberDataKey"/>) because
    /// Steam cannot enumerate member-data keys — only the local player writes
    /// their own blob. Steam caps member data at 8KB.
    /// </summary>
    public class SteamMetadata : LobbyMetadataBase
    {
        private readonly SteamLobby _lobby;
        private readonly bool _isPlayerMetadata;
        private readonly bool _isLocalPlayer;

        internal SteamMetadata(SteamLobby lobby, bool isPlayerMetadata, bool isLocalPlayer)
        {
            _lobby = lobby;
            _isPlayerMetadata = isPlayerMetadata;
            _isLocalPlayer = isLocalPlayer;
        }

        protected override bool CanWrite => _isPlayerMetadata ? _isLocalPlayer : _lobby.isOwner;

        protected override void PushChange(string key, string value)
        {
            var lobbyId = _lobby.steamLobbyId;

            if (_isPlayerMetadata)
            {
                SteamMatchmaking.SetLobbyMemberData(lobbyId, SteamLobby.MemberDataKey, SerializeBlob(Snapshot()));
            }
            else
            {
                if (value == null)
                    SteamMatchmaking.DeleteLobbyData(lobbyId, key);
                else
                    SteamMatchmaking.SetLobbyData(lobbyId, key, value);
            }
        }

        [Serializable]
        private struct BlobEntry
        {
            public string k;
            public string v;
        }

        [Serializable]
        private struct Blob
        {
            public BlobEntry[] entries;
        }

        internal static string SerializeBlob(IReadOnlyDictionary<string, string> data)
        {
            var blob = new Blob { entries = new BlobEntry[data.Count] };
            int i = 0;
            foreach (var kvp in data)
                blob.entries[i++] = new BlobEntry { k = kvp.Key, v = kvp.Value };
            return JsonUtility.ToJson(blob);
        }

        internal static Dictionary<string, string> DeserializeBlob(string json)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(json))
                return result;

            try
            {
                var blob = JsonUtility.FromJson<Blob>(json);
                if (blob.entries != null)
                {
                    foreach (var entry in blob.entries)
                    {
                        if (!string.IsNullOrEmpty(entry.k))
                            result[entry.k] = entry.v;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamMetadata] Failed to parse member data blob: {e.Message}");
            }

            return result;
        }
    }
}
#endif
