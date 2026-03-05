using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PurrLobby.Utils;
using UnityEngine;

namespace PurrLobby
{
    internal sealed class PurrLobby : ILobby
    {
        readonly LobbyApiClient _api;
        readonly string _localPlayerId;
        readonly string _playerToken;
        readonly float _pollInterval;

        readonly List<PurrPlayer> _playersList = new List<PurrPlayer>();
        readonly Dictionary<string, PurrPlayer> _playersById = new Dictionary<string, PurrPlayer>();
        readonly PurrMetadata _lobbyMetadata = new PurrMetadata();
        readonly PurrLobbyChat _chat;

        string _hostPlayerId;
        bool _disposed;
        int _version;
        string _state; // "waiting", "starting", "started"

        public string id { get; }
        public int maxPlayers { get; private set; }
        public IReadOnlyList<IPlayer> players => _playersList;
        public IMetadata lobbyData => _lobbyMetadata;
        public ILobbyChat chat => _chat;

        public IPlayer localPlayer
        {
            get
            {
                _playersById.TryGetValue(_localPlayerId, out var p);
                return p;
            }
        }

        public IPlayer host
        {
            get
            {
                _playersById.TryGetValue(_hostPlayerId, out var p);
                return p;
            }
        }

        public event Action<IPlayer> onPlayerJoined;
        public event Action<IPlayer> onPlayerLeft;
        public event Action<IPlayer> onHostChanged;
        public event Action onLobbyDestroyed;

        internal PurrLobby(
            LobbyApiClient api,
            string lobbyId,
            string localPlayerId,
            string playerToken,
            JObject lobbyData,
            float pollInterval = 1.5f)
        {
            _api = api;
            id = lobbyId;
            _localPlayerId = localPlayerId;
            _playerToken = playerToken;
            _pollInterval = pollInterval;

            _chat = new PurrLobbyChat(api, lobbyId, playerToken, ResolvePlayer);

            // Wire up metadata patching to server
            _lobbyMetadata.onPatchRequested = patch => PatchLobbyMetadataAsync(patch).Forget();

            // Apply initial lobby data if available
            if (lobbyData != null)
                ApplyLobbyCore(lobbyData);
        }

        /// <summary>
        /// Apply the full initial snapshot and start polling.
        /// Called by PurrLobbyProvider after the first poll succeeds.
        /// </summary>
        internal void ApplyInitialSnapshot(JObject snapshot)
        {
            ApplySnapshot(snapshot, isInitial: true);
            PollLoopAsync().Forget();
        }

        void ApplyLobbyCore(JObject data)
        {
            _hostPlayerId = data.GetString("hostPlayerId", _hostPlayerId);
            maxPlayers = data.GetInt("maxPlayers", maxPlayers);
            _state = data.GetString("state", _state);
            _version = data.GetInt("version", _version);
        }

        void ApplySnapshot(JObject snapshot, bool isInitial = false)
        {
            var lobbyObj = snapshot.GetObject("lobby");
            if (lobbyObj == null) return;

            string oldHost = _hostPlayerId;
            ApplyLobbyCore(lobbyObj);

            // Process players
            var playersArray = snapshot.GetArray("players");
            if (playersArray != null)
                ApplyPlayers(playersArray, isInitial);

            // Process lobby metadata (only if key is present in snapshot to avoid wiping data)
            if (snapshot.TryGetValue("metadata", out _))
            {
                var meta = snapshot.GetStringDict("metadata");
                if (isInitial)
                    _lobbyMetadata.SetAllSilent(meta);
                else
                    _lobbyMetadata.ApplySnapshot(meta);
            }

            // Process player metadata
            var playerMeta = snapshot.GetObject("playerMetadata");
            if (playerMeta != null)
            {
                foreach (var kv in playerMeta)
                {
                    if (_playersById.TryGetValue(kv.Key, out var player) &&
                        player.userData is PurrMetadata pm &&
                        kv.Value is JObject pmObj)
                    {
                        var strDict = new Dictionary<string, string>();
                        foreach (var entry in pmObj)
                            strDict[entry.Key] = entry.Value?.ToString() ?? string.Empty;

                        if (isInitial)
                            pm.SetAllSilent(strDict);
                        else
                            pm.ApplySnapshot(strDict);
                    }
                }
            }

            // Process chat
            var chatArray = snapshot.GetArray("chat");
            _chat.ProcessMessages(chatArray);

            // Fire events (skip on initial)
            if (!isInitial)
            {
                if (oldHost != _hostPlayerId && _playersById.TryGetValue(_hostPlayerId, out var newHost))
                    onHostChanged?.Invoke(newHost);
            }
        }

        void ApplyPlayers(JArray playersArray, bool isInitial)
        {
            var incoming = new HashSet<string>();

            foreach (var item in playersArray)
            {
                if (item is not JObject pd) continue;

                string pid = pd.GetString("id");
                if (pid == null) continue;
                incoming.Add(pid);

                bool isPlayerHost = pid == _hostPlayerId;

                if (_playersById.TryGetValue(pid, out var existing))
                {
                    existing.displayName = pd.GetString("displayName", existing.displayName);
                    existing.isHost = isPlayerHost;
                    existing.lastSeen = pd.GetLong("lastSeen", existing.lastSeen);
                }
                else
                {
                    var newPlayer = new PurrPlayer(
                        pid,
                        pd.GetString("displayName", pid),
                        isPlayerHost,
                        pd.GetLong("joinedAt"),
                        pd.GetLong("lastSeen")
                    );
                    _playersById[pid] = newPlayer;
                    _playersList.Add(newPlayer);

                    // Wire metadata patching for the local player
                    if (pid == _localPlayerId)
                        WirePlayerMetadata(newPlayer);

                    if (!isInitial)
                        onPlayerJoined?.Invoke(newPlayer);
                }
            }

            // Detect removed players
            for (int i = _playersList.Count - 1; i >= 0; i--)
            {
                var p = _playersList[i];
                if (!incoming.Contains(p.id))
                {
                    _playersList.RemoveAt(i);
                    _playersById.Remove(p.id);

                    if (!isInitial)
                        onPlayerLeft?.Invoke(p);
                }
            }

            // Update host flags
            foreach (var p in _playersList)
                p.isHost = p.id == _hostPlayerId;
        }

        IPlayer ResolvePlayer(string playerId)
        {
            _playersById.TryGetValue(playerId, out var p);
            return p;
        }

        // ── Actions ──────────────────────────────────────────────────

        public void InvitePlayer(IPlayer player)
        {
            throw new NotSupportedException("Invite is not supported by the PurrLobby REST backend. Share the lobby code instead.");
        }

        public void KickPlayer(IPlayer player)
        {
            KickPlayerAsync(player).Forget();
        }

        public void LeaveLobby()
        {
            _disposed = true;
            LeaveAsync().Forget();
        }

        async Task KickPlayerAsync(IPlayer player)
        {
            await _api.PostAsync($"/api/lobby/{id}/kick", new Dictionary<string, object>
            {
                { "playerId", player.id }
            }, _playerToken);
        }

        async Task LeaveAsync()
        {
            await _api.PostAsync($"/api/lobby/{id}/leave", playerToken: _playerToken);
        }

        // ── Metadata patching ────────────────────────────────────────

        async Task PatchLobbyMetadataAsync(Dictionary<string, string> patch)
        {
            await _api.PatchAsync($"/api/lobby/{id}/metadata", new Dictionary<string, object>
            {
                { "metadata", patch }
            }, _playerToken);
        }

        internal void WirePlayerMetadata(PurrPlayer player)
        {
            if (player.userData is PurrMetadata pm)
            {
                pm.onPatchRequested = patch => PatchPlayerMetadataAsync(patch).Forget();
            }
        }

        async Task PatchPlayerMetadataAsync(Dictionary<string, string> patch)
        {
            await _api.PatchAsync($"/api/lobby/{id}/player", new Dictionary<string, object>
            {
                { "metadata", patch }
            }, _playerToken);
        }

        // ── Polling ──────────────────────────────────────────────────

        async Task PollLoopAsync()
        {
            while (!_disposed)
            {
                try
                {
                    // WebGL-safe delay (Task.Delay uses System.Threading.Timer which breaks on WebGL)
                    await TaskUtils.DelaySeconds(_pollInterval);
                    if (_disposed) break;

                    var queryParams = new Dictionary<string, string>
                    {
                        { "chatAfterSeq", _chat.lastSeq.ToString() }
                    };

                    string response = await _api.GetAsync($"/api/lobby/{id}", queryParams, _playerToken);
                    var snapshot = Json.ParseObject(response);
                    if (snapshot != null)
                        ApplySnapshot(snapshot);
                }
                catch (LobbyApiException ex) when (ex.StatusCode == 404)
                {
                    _disposed = true;
                    onLobbyDestroyed?.Invoke();
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PurrLobby] Poll error: {ex.Message}");
                }
            }
        }

        internal void Dispose()
        {
            _disposed = true;
        }
    }
}
