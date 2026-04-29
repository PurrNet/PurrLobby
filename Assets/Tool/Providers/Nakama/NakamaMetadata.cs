#if NAKAMA
using System;
using System.Collections.Generic;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Metadata bag backed by Nakama match-state messages.
    ///
    /// For lobby metadata: only the host's writes are authoritative — non-host writes are sent as a request
    /// and the host echoes the patch back to everyone.
    ///
    /// For player metadata: each player owns their own bag. Non-local writes are silently ignored — the local
    /// player's writes broadcast a patch that everyone applies.
    /// </summary>
    public class NakamaMetadata : IMetadata
    {
        private readonly NakamaLobby _lobby;
        private readonly string _ownerId; // null = lobby metadata, non-null = player metadata
        private readonly bool _isLocalOwner;
        private readonly Dictionary<string, string> _data = new();

        public event Action<string, string> onDataChanged;

        internal NakamaMetadata(NakamaLobby lobby, string ownerId, bool isLocalOwner)
        {
            _lobby = lobby;
            _ownerId = ownerId;
            _isLocalOwner = isLocalOwner;
        }

        public void SetData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (_data.TryGetValue(key, out var existing) && existing == value)
                return;

            if (_ownerId == null && !_lobby.isOwner)
            {
                UnityEngine.Debug.LogWarning("[NakamaMetadata] Lobby metadata can only be written by the host.");
                return;
            }
            if (_ownerId != null && !_isLocalOwner)
            {
                UnityEngine.Debug.LogWarning("[NakamaMetadata] Other players' metadata is read-only.");
                return;
            }

            _data[key] = value;
            onDataChanged?.Invoke(key, value);

            if (_ownerId == null)
                _lobby.SubmitLobbyMetadataPatch(new Dictionary<string, string> { { key, value } });
            else
                _lobby.BroadcastLocalPlayerMetadata(new Dictionary<string, string>(_data));
        }

        public string GetData(string key) => _data[key];

        public bool TryGetData(string key, out string value) => _data.TryGetValue(key, out value);

        public void RemoveData(string key)
        {
            if (_ownerId == null && !_lobby.isOwner)
            {
                UnityEngine.Debug.LogWarning("[NakamaMetadata] Lobby metadata can only be written by the host.");
                return;
            }
            if (_ownerId != null && !_isLocalOwner)
            {
                UnityEngine.Debug.LogWarning("[NakamaMetadata] Other players' metadata is read-only.");
                return;
            }
            if (!_data.Remove(key))
                return;

            onDataChanged?.Invoke(key, null);

            if (_ownerId == null)
                _lobby.SubmitLobbyMetadataPatch(new Dictionary<string, string> { { key, null } });
            else
                _lobby.BroadcastLocalPlayerMetadata(new Dictionary<string, string>(_data));
        }

        public bool ContainsData(string key) => _data.ContainsKey(key);

        internal IReadOnlyDictionary<string, string> Snapshot() => _data;

        /// <summary>
        /// Replaces the bag wholesale and fires <see cref="onDataChanged"/> for any key that changed,
        /// added or was removed. Used when a snapshot or full replacement arrives over the wire.
        /// </summary>
        internal void ReplaceFrom(Dictionary<string, string> replacement)
        {
            replacement ??= new Dictionary<string, string>();

            // Removed keys.
            _scratchKeys.Clear();
            foreach (var key in _data.Keys)
            {
                if (!replacement.ContainsKey(key))
                    _scratchKeys.Add(key);
            }
            for (int i = 0; i < _scratchKeys.Count; i++)
            {
                _data.Remove(_scratchKeys[i]);
                onDataChanged?.Invoke(_scratchKeys[i], null);
            }
            _scratchKeys.Clear();

            // Added / changed keys.
            foreach (var kvp in replacement)
            {
                if (_data.TryGetValue(kvp.Key, out var existing) && existing == kvp.Value)
                    continue;
                _data[kvp.Key] = kvp.Value;
                onDataChanged?.Invoke(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Applies a partial patch — null values delete the key.
        /// </summary>
        internal void ApplyPatch(Dictionary<string, string> patch)
        {
            if (patch == null)
                return;

            foreach (var kvp in patch)
            {
                if (kvp.Value == null)
                {
                    if (_data.Remove(kvp.Key))
                        onDataChanged?.Invoke(kvp.Key, null);
                }
                else
                {
                    if (_data.TryGetValue(kvp.Key, out var existing) && existing == kvp.Value)
                        continue;
                    _data[kvp.Key] = kvp.Value;
                    onDataChanged?.Invoke(kvp.Key, kvp.Value);
                }
            }
        }

        private static readonly List<string> _scratchKeys = new();
    }
}
#endif
