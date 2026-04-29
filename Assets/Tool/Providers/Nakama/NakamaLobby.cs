#if NAKAMA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using Newtonsoft.Json;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Wraps a Nakama relayed match as an <see cref="ILobby"/>.
    /// </summary>
    public class NakamaLobby : ILobby, IDisposable
    {
        public string id => _matchId;
        public string joinCode => _code;
        public IPlayer localPlayer => _localPlayer;
        public IPlayer owner => _host;
        public int maxPlayers => _maxPlayers;
        public IReadOnlyList<IPlayer> players => _players;
        public IMetadata lobbyData => _lobbyMetadata;
        public bool isLobbyJoinable => _joinable;
        public ILobbyChat chat => _chat;
        public bool isOwner => _localPlayer != null && _localPlayer.isOwner;

        // onPlayerJoined and onHostChanged use replay-on-subscribe semantics: when a new handler
        // attaches, it is immediately invoked for every existing player / the current host. The
        // alternative — a one-shot deferred fire from the constructor — races with awaits that
        // happen between `new NakamaLobby(...)` and the consumer's Setup call (e.g. JoinLobby's
        // AwaitFirstSnapshotAsync), where Unity's message pump can drain the deferred fire before
        // the LobbyView ever subscribes.
        private Action<IPlayer> _onPlayerJoined;
        public event Action<IPlayer> onPlayerJoined
        {
            add
            {
                _onPlayerJoined += value;
                if (value == null)
                    return;
                for (int i = 0; i < _players.Count; i++)
                    value.Invoke(_players[i]);
            }
            remove => _onPlayerJoined -= value;
        }

        private Action<IPlayer> _onHostChanged;
        public event Action<IPlayer> onOwnerChanged
        {
            add
            {
                _onHostChanged += value;
                if (value != null && _host != null)
                    value.Invoke(_host);
            }
            remove => _onHostChanged -= value;
        }

        public event Action<IPlayer> onPlayerLeft;
        public event Action<IPlayer> onPlayerUpdated;
        public event Action onLobbyDestroyed;

        private readonly ISocket _socket;
        private readonly ISession _session;
        private readonly string _matchId;

        private string _code;
        private string _name;
        private int _maxPlayers;
        private bool _joinable;
        private string _hostUserId;

        private NakamaPlayer _localPlayer;
        private NakamaPlayer _host;
        private readonly List<NakamaPlayer> _players = new();
        private readonly Dictionary<string, string> _displayNames = new();
        private readonly NakamaMetadata _lobbyMetadata;
        private readonly NakamaChat _chat;

        private bool _disposed;
        private bool _firstSnapshotReceived;
        private TaskCompletionSource<bool> _firstSnapshotTcs;
        private readonly object _snapshotGate = new();

        internal NakamaLobby(ISession session,
            ISocket socket,
            IMatch match,
            string code,
            string name,
            int maxPlayers,
            string hostUserId,
            Dictionary<string, string> initialMetadata)
        {
            _session = session;
            _socket = socket;
            _matchId = match.Id;
            _code = code;
            _name = name;
            _maxPlayers = maxPlayers;
            _joinable = true;
            // null means "owner unknown; waiting for snapshot" — don't fall back to self,
            // that would briefly mark the joiner as owner and skew the kick / metadata gates.
            _hostUserId = hostUserId;

            _lobbyMetadata = new NakamaMetadata(this, ownerId: null, isLocalOwner: true);
            _chat = new NakamaChat(this);

            if (initialMetadata != null)
                _lobbyMetadata.ReplaceFrom(initialMetadata);

            // Seed local player + any presences already in the match.
            SeedFromMatch(match);

            _socket.ReceivedMatchPresence += OnMatchPresence;
            _socket.ReceivedMatchState += OnMatchState;
            _socket.Closed += OnSocketDisconnect;
        }

        private void SeedFromMatch(IMatch match)
        {
            var selfDisplay = !string.IsNullOrEmpty(match.Self?.Username) ? match.Self.Username : _session.Username;
            var selfId = match.Self?.UserId ?? _session.UserId;
            var selfPlayer = new NakamaPlayer(this, selfId, selfDisplay, isHost: selfId == _hostUserId, isLocal: true);
            _players.Add(selfPlayer);
            _displayNames[selfId] = selfDisplay;
            _localPlayer = selfPlayer;
            if (selfPlayer.isOwner)
                _host = selfPlayer;

            if (match.Presences != null)
            {
                foreach (var p in match.Presences)
                {
                    if (p == null || p.UserId == selfId)
                        continue;
                    if (TryFindPlayer(p.UserId, out _))
                        continue;
                    var isPlayerHost = p.UserId == _hostUserId;
                    var player = new NakamaPlayer(this, p.UserId, p.Username, isPlayerHost, isLocal: false);
                    _players.Add(player);
                    _displayNames[p.UserId] = p.Username;
                    if (isPlayerHost)
                        _host = player;
                }
            }
        }

        public void KickPlayer(IPlayer player)
        {
            if (player == null)
                return;
            if (!isOwner)
            {
                Debug.LogWarning("[NakamaLobby] Only the host can kick players.");
                return;
            }
            _ = SendMatchStateAsync(NakamaOpCodes.Kick, new KickMessage { userId = player.id });
        }

        public void SetIsLobbyJoinable(bool isJoinable)
        {
            if (!isOwner)
            {
                Debug.LogWarning("[NakamaLobby] Only the host can change joinability.");
                return;
            }
            if (_joinable == isJoinable)
                return;
            _joinable = isJoinable;
            _ = SendMatchStateAsync(NakamaOpCodes.SetJoinable, new JoinableMessage { joinable = isJoinable });
        }

        /// <summary>
        /// Used by the join paths to block the consumer until the owner's authoritative snapshot
        /// lands — so by the time anyone holds an <see cref="ILobby"/> from a join call, the owner
        /// identity and lobby metadata reflect the creator's truth instead of a local guess.
        /// </summary>
        internal Task AwaitFirstSnapshotAsync(int timeoutMs)
        {
            TaskCompletionSource<bool> tcs;
            lock (_snapshotGate)
            {
                if (_firstSnapshotReceived)
                    return Task.CompletedTask;
                if (_disposed)
                    return Task.FromException(new ObjectDisposedException(nameof(NakamaLobby)));
                // Joined into a match with no other presences — no one to author a snapshot.
                // The lobby is effectively dead; fail rather than waiting out the full timeout.
                if (_players.Count <= 1)
                    return Task.FromException(new InvalidOperationException(
                        "Joined Nakama lobby has no other presences; the owner is gone."));
                tcs = _firstSnapshotTcs ??= new TaskCompletionSource<bool>();
            }
            _ = SendMatchStateBytesAsync(NakamaOpCodes.RequestSnapshot, null);
            return AwaitWithTimeoutAsync(tcs.Task, timeoutMs);
        }

        private static async Task AwaitWithTimeoutAsync(Task task, int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                await task;
                return;
            }
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
                throw new TimeoutException("Timed out waiting for first Nakama lobby snapshot.");
            await task;
        }

        private void SetSnapshotReceived()
        {
            TaskCompletionSource<bool> tcs;
            lock (_snapshotGate)
            {
                if (_firstSnapshotReceived)
                    return;
                _firstSnapshotReceived = true;
                tcs = _firstSnapshotTcs;
            }
            tcs?.TrySetResult(true);
        }

        private void SetSnapshotFailed(Exception ex)
        {
            TaskCompletionSource<bool> tcs;
            lock (_snapshotGate)
            {
                if (_firstSnapshotReceived)
                    return;
                // Mark received so callers that await *after* failure also surface the error
                // (via a completed-with-exception task) instead of hanging indefinitely.
                _firstSnapshotReceived = true;
                tcs = _firstSnapshotTcs;
            }
            tcs?.TrySetException(ex);
        }

        public async void LeaveLobby()
        {
            try
            {
                if (_disposed)
                    return;

                await _socket.LeaveMatchAsync(_matchId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NakamaLobby] LeaveMatch failed: {ex.Message}");
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            // Surface the disposal to anyone awaiting a first snapshot so they don't hang.
            SetSnapshotFailed(new ObjectDisposedException(nameof(NakamaLobby)));
            _socket.ReceivedMatchPresence -= OnMatchPresence;
            _socket.ReceivedMatchState -= OnMatchState;
            _socket.Closed -= OnSocketDisconnect;
        }

        internal void SubmitLobbyMetadataPatch(Dictionary<string, string> patch)
        {
            if (patch == null || patch.Count == 0 || !isOwner)
                return;
            _ = SendMatchStateAsync(NakamaOpCodes.LobbyMetadataPatch, new LobbyMetadataMessage { metadata = patch });
        }

        internal void BroadcastLocalPlayerMetadata(Dictionary<string, string> snapshot)
        {
            _ = SendMatchStateAsync(NakamaOpCodes.PlayerMetadataPatch, new PlayerMetadataMessage
            {
                userId = _localPlayer?.id ?? _session.UserId,
                metadata = snapshot
            });
        }

        internal Task SendMatchStateBytesAsync(long opCode, byte[] data)
        {
            if (_disposed || !_socket.IsConnected)
                return Task.CompletedTask;
            return _socket.SendMatchStateAsync(_matchId, opCode, data ?? Array.Empty<byte>());
        }

        private Task SendMatchStateAsync<T>(long opCode, T payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            return SendMatchStateBytesAsync(opCode, bytes);
        }

        private void OnMatchPresence(IMatchPresenceEvent evt)
        {
            if (evt == null || evt.MatchId != _matchId)
                return;

            if (evt.Joins != null)
            {
                foreach (var presence in evt.Joins)
                {
                    if (presence == null || presence.UserId == _session.UserId)
                        continue;
                    if (TryFindPlayer(presence.UserId, out _))
                        continue;

                    var isPlayerHost = presence.UserId == _hostUserId;
                    var player = new NakamaPlayer(this, presence.UserId, presence.Username, isPlayerHost, isLocal: false);
                    _players.Add(player);
                    _displayNames[presence.UserId] = presence.Username;

                    _onPlayerJoined?.Invoke(player);

                    if (isPlayerHost)
                    {
                        _host = player;
                        _onHostChanged?.Invoke(player);
                    }

                    // The host pushes a full snapshot to every newcomer so they can hydrate state.
                    if (this.isOwner)
                        _ = SendSnapshotAsync();
                }
            }

            if (evt.Leaves != null)
            {
                bool anyLeaves = false;
                foreach (var presence in evt.Leaves)
                {
                    if (presence == null)
                        continue;
                    if (!TryRemovePlayer(presence.UserId, out var removed))
                        continue;

                    anyLeaves = true;
                    onPlayerLeft?.Invoke(removed);

                    if (removed.isOwner)
                        HandleHostDisappeared();
                }

                // During the join await window we don't trust isOwner flags (the real owner is
                // unknown until snapshot arrives). If a leave drained the match down to just us,
                // there's no peer left to author a snapshot — fail fast instead of waiting out
                // the full timeout.
                if (anyLeaves && !_firstSnapshotReceived && _players.Count <= 1)
                    SetSnapshotFailed(new InvalidOperationException(
                        "Nakama lobby drained while waiting for the first snapshot."));
            }
        }

        private void OnMatchState(IMatchState state)
        {
            if (state == null || state.MatchId != _matchId)
                return;

            try
            {
                switch (state.OpCode)
                {
                    case NakamaOpCodes.Snapshot:
                        ApplySnapshot(DecodePayload<SnapshotMessage>(state.State));
                        break;
                    case NakamaOpCodes.LobbyMetadataPatch:
                        ApplyLobbyMetadataPatch(DecodePayload<LobbyMetadataMessage>(state.State));
                        break;
                    case NakamaOpCodes.PlayerMetadataPatch:
                        ApplyPlayerMetadataPatch(DecodePayload<PlayerMetadataMessage>(state.State));
                        break;
                    case NakamaOpCodes.Chat:
                        if (TryFindPlayer(state.UserPresence?.UserId, out var sender))
                            _chat.DispatchIncoming(sender, state.State);
                        break;
                    case NakamaOpCodes.Kick:
                        ApplyKick(DecodePayload<KickMessage>(state.State));
                        break;
                    case NakamaOpCodes.SetJoinable:
                        ApplySetJoinable(DecodePayload<JoinableMessage>(state.State));
                        break;
                    case NakamaOpCodes.HostMigration:
                        ApplyHostMigration(DecodePayload<HostMigrationMessage>(state.State));
                        break;
                    case NakamaOpCodes.RequestSnapshot:
                        if (this.isOwner)
                            _ = SendSnapshotAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnSocketDisconnect(string reason)
        {
            if (_disposed)
                return;
            SetSnapshotFailed(new Exception($"Nakama socket closed: {reason}"));
            onLobbyDestroyed?.Invoke();
            Dispose();
        }

        private void ApplySnapshot(SnapshotMessage msg)
        {
            if (msg == null)
                return;

            SetSnapshotReceived();

            _name = msg.lobbyName;
            _code = msg.code;
            _maxPlayers = msg.maxPlayers > 0 ? msg.maxPlayers : _maxPlayers;
            _joinable = msg.joinable;

            if (msg.displayNames != null)
            {
                foreach (var kvp in msg.displayNames)
                    _displayNames[kvp.Key] = kvp.Value;
            }

            // Host changes are conveyed by the snapshot's hostUserId.
            if (!string.IsNullOrEmpty(msg.hostUserId) && msg.hostUserId != _hostUserId)
                ChangeHost(msg.hostUserId);

            if (msg.metadata != null)
                _lobbyMetadata.ReplaceFrom(msg.metadata);

            if (msg.playerMetadata != null)
            {
                foreach (var kvp in msg.playerMetadata)
                {
                    if (!TryFindPlayer(kvp.Key, out var player))
                        continue;
                    player.GetMetadata().ReplaceFrom(kvp.Value);
                    player.TriggerOnPlayerMetadataUpdated();
                    onPlayerUpdated?.Invoke(player);
                }
            }
        }

        private void ApplyLobbyMetadataPatch(LobbyMetadataMessage msg)
        {
            if (msg?.metadata == null)
                return;
            _lobbyMetadata.ApplyPatch(msg.metadata);
        }

        private void ApplyPlayerMetadataPatch(PlayerMetadataMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.userId))
                return;
            if (!TryFindPlayer(msg.userId, out var player))
                return;
            player.GetMetadata().ReplaceFrom(msg.metadata);
            player.TriggerOnPlayerMetadataUpdated();
            onPlayerUpdated?.Invoke(player);
        }

        private void ApplyKick(KickMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.userId))
                return;
            if (msg.userId == _session.UserId)
            {
                onLobbyDestroyed?.Invoke();
                _ = LeaveQuietlyAsync();
            }
        }

        private void ApplySetJoinable(JoinableMessage msg)
        {
            if (msg == null)
                return;
            _joinable = msg.joinable;
        }

        private void ApplyHostMigration(HostMigrationMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.hostUserId))
                return;

            // Defend against stale migrations: if the named host isn't a current presence,
            // accepting would null out _host while leaving _hostUserId pointing at a ghost,
            // and we'd block all owner-gated ops (kick, joinable, metadata) until another
            // migration arrived. Skip; the next valid migration or snapshot will repair us.
            // Snapshots take the other path (ChangeHost direct) because initial hydration
            // can legitimately reference presences whose join events haven't landed yet.
            if (!TryFindPlayer(msg.hostUserId, out _))
                return;

            ChangeHost(msg.hostUserId);

            // Edge case: another peer's HandleHostDisappeared elected us as the new owner mid-join,
            // before we ever received the original creator's snapshot. The electing peer doesn't
            // send a snapshot (it's not the new owner) — we are the authoritative source now, so
            // satisfy our own pending await and broadcast our state to everyone else.
            if (msg.hostUserId == _session.UserId && !_firstSnapshotReceived)
            {
                SetSnapshotReceived();
                _ = SendSnapshotAsync();
            }
        }

        private void HandleHostDisappeared()
        {
            // Deterministically elect the lowest user id among remaining players.
            if (_players.Count == 0)
            {
                onLobbyDestroyed?.Invoke();
                return;
            }

            var newHostId = _players
                .Select(p => p.id)
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s, StringComparer.Ordinal)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(newHostId))
                return;

            ChangeHost(newHostId);

            // The newly-elected host announces the migration and re-broadcasts state.
            if (newHostId == _session.UserId)
            {
                _ = SendMatchStateAsync(NakamaOpCodes.HostMigration, new HostMigrationMessage { hostUserId = newHostId });
                _ = SendSnapshotAsync();
            }
        }

        private void ChangeHost(string newHostUserId)
        {
            if (_hostUserId == newHostUserId)
                return;

            _hostUserId = newHostUserId;

            // Resolve the new host against our current player list. May be null if the announcer
            // saw a presence we haven't observed yet — in that case OnMatchPresence will backfill
            // _host when the join event arrives (it checks presence.UserId == _hostUserId).
            NakamaPlayer resolved = null;
            if (!string.IsNullOrEmpty(newHostUserId))
                TryFindPlayer(newHostUserId, out resolved);

            // Reset before walking so a stale _host (e.g. from a previous owner that we've since
            // dropped from _players) doesn't linger when the new id can't yet be resolved.
            _host = resolved;

            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                var shouldBeHost = resolved != null && p == resolved;
                if (p.isOwner != shouldBeHost)
                {
                    p.SetIsHost(shouldBeHost);
                    p.TriggerOnPlayerUpdated();
                    onPlayerUpdated?.Invoke(p);
                }
            }

            if (resolved != null)
                _onHostChanged?.Invoke(resolved);
        }

        private async Task LeaveQuietlyAsync()
        {
            try { await _socket.LeaveMatchAsync(_matchId); }
            catch { /* swallow */ }
            Dispose();
        }

        private Task SendSnapshotAsync()
        {
            if (!isOwner)
                return Task.CompletedTask;

            var snapshot = new SnapshotMessage
            {
                hostUserId = _hostUserId,
                lobbyName = _name,
                code = _code,
                maxPlayers = _maxPlayers,
                joinable = _joinable,
                metadata = new Dictionary<string, string>(_lobbyMetadata.Snapshot()),
                playerMetadata = new Dictionary<string, Dictionary<string, string>>(),
                displayNames = new Dictionary<string, string>(_displayNames),
            };

            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                snapshot.playerMetadata[p.id] = new Dictionary<string, string>(p.GetMetadata().Snapshot());
            }

            return SendMatchStateAsync(NakamaOpCodes.Snapshot, snapshot);
        }

        private bool TryFindPlayer(string userId, out NakamaPlayer player)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].id == userId)
                {
                    player = _players[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        private bool TryRemovePlayer(string userId, out NakamaPlayer removed)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].id == userId)
                {
                    removed = _players[i];
                    _players.RemoveAt(i);
                    return true;
                }
            }
            removed = null;
            return false;
        }

        private static T DecodePayload<T>(byte[] state) where T : class
        {
            if (state == null || state.Length == 0)
                return null;
            var json = Encoding.UTF8.GetString(state);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
#endif
