#if NAKAMA
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using PurrNet.Packing;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Wraps a Nakama relayed match as an <see cref="ILobby"/>.</summary>
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

        // Replay-on-subscribe: new handlers are immediately invoked for existing players/host
        // to avoid race conditions between construction and the consumer's Setup call.
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
            // null = owner unknown until snapshot arrives; falling back to self would mis-gate kick/metadata
            _hostUserId = hostUserId;

            _lobbyMetadata = new NakamaMetadata(this, ownerId: null, isLocalOwner: true);
            _chat = new NakamaChat(this);

            if (initialMetadata != null)
                _lobbyMetadata.ReplaceFrom(initialMetadata);

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

        /// <summary>Blocks until the owner's authoritative snapshot arrives.</summary>
        internal Task AwaitFirstSnapshotAsync(int timeoutMs)
        {
            TaskCompletionSource<bool> tcs;
            lock (_snapshotGate)
            {
                if (_firstSnapshotReceived)
                    return Task.CompletedTask;
                if (_disposed)
                    return Task.FromException(new ObjectDisposedException(nameof(NakamaLobby)));
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

        private Task SendMatchStateAsync<T>(long opCode, T payload) where T : struct, IPackedAuto
        {
            if (_disposed || !_socket.IsConnected)
                return Task.CompletedTask;

            using var packer = BitPackerPool.Get();
            Packer<T>.Write(packer, payload);
            var bytes = packer.ToByteData().span.ToArray();
            return _socket.SendMatchStateAsync(_matchId, opCode, bytes);
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

                // If all other players left before snapshot arrived, fail fast
                lock (_snapshotGate)
                {
                    if (anyLeaves && !_firstSnapshotReceived && _players.Count <= 1)
                        SetSnapshotFailed(new InvalidOperationException(
                            "Nakama lobby drained while waiting for the first snapshot."));
                }
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
                        ApplySnapshot(Decode<SnapshotMessage>(state.State));
                        break;
                    case NakamaOpCodes.LobbyMetadataPatch:
                        ApplyLobbyMetadataPatch(Decode<LobbyMetadataMessage>(state.State));
                        break;
                    case NakamaOpCodes.PlayerMetadataPatch:
                        ApplyPlayerMetadataPatch(Decode<PlayerMetadataMessage>(state.State));
                        break;
                    case NakamaOpCodes.Chat:
                        if (TryFindPlayer(state.UserPresence?.UserId, out var sender))
                            _chat.DispatchIncoming(sender, state.State);
                        break;
                    case NakamaOpCodes.Kick:
                        ApplyKick(Decode<KickMessage>(state.State));
                        break;
                    case NakamaOpCodes.SetJoinable:
                        ApplySetJoinable(Decode<JoinableMessage>(state.State));
                        break;
                    case NakamaOpCodes.HostMigration:
                        ApplyHostMigration(Decode<HostMigrationMessage>(state.State));
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
            if (msg.metadata == null)
                return;
            _lobbyMetadata.ApplyPatch(msg.metadata);
        }

        private void ApplyPlayerMetadataPatch(PlayerMetadataMessage msg)
        {
            if (string.IsNullOrEmpty(msg.userId))
                return;
            if (!TryFindPlayer(msg.userId, out var player))
                return;
            player.GetMetadata().ReplaceFrom(msg.metadata);
            player.TriggerOnPlayerMetadataUpdated();
            onPlayerUpdated?.Invoke(player);
        }

        private void ApplyKick(KickMessage msg)
        {
            if (string.IsNullOrEmpty(msg.userId))
                return;
            if (msg.userId == _session.UserId)
            {
                onLobbyDestroyed?.Invoke();
                _ = LeaveQuietlyAsync();
            }
        }

        private void ApplySetJoinable(JoinableMessage msg)
        {
            _joinable = msg.joinable;
        }

        private void ApplyHostMigration(HostMigrationMessage msg)
        {
            if (string.IsNullOrEmpty(msg.hostUserId))
                return;

            // Skip stale migrations where the named host isn't a current presence
            if (!TryFindPlayer(msg.hostUserId, out _))
                return;

            ChangeHost(msg.hostUserId);

            // We were elected owner before receiving the first snapshot — become authoritative now
            lock (_snapshotGate)
            {
                if (msg.hostUserId == _session.UserId && !_firstSnapshotReceived)
                {
                    SetSnapshotReceived();
                    _ = SendSnapshotAsync();
                }
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

            string newHostId = null;
            for (int i = 0; i < _players.Count; i++)
            {
                var pid = _players[i].id;
                if (string.IsNullOrEmpty(pid))
                    continue;
                if (newHostId == null || string.CompareOrdinal(pid, newHostId) < 0)
                    newHostId = pid;
            }

            if (string.IsNullOrEmpty(newHostId))
                return;

            ChangeHost(newHostId);

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

            // May be null if the presence hasn't arrived yet — OnMatchPresence will backfill
            NakamaPlayer resolved = null;
            if (!string.IsNullOrEmpty(newHostUserId))
                TryFindPlayer(newHostUserId, out resolved);

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

        private static T Decode<T>(byte[] state) where T : struct, IPackedAuto
        {
            if (state == null || state.Length == 0)
                return default;
            using var packer = BitPackerPool.Get(state);
            return Packer<T>.Read(packer);
        }
    }
}
#endif
