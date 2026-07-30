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
    public class NakamaLobby : LobbyBase<NakamaPlayer>, IDisposable
    {
        public override string id => _matchId;
        public override int maxPlayers => _maxPlayers;
        public override IMetadata lobbyData => _lobbyMetadata;
        public override bool isLobbyJoinable => _joinable;
        public override ILobbyChat chat => _chat;

        private readonly ISocket _socket;
        private readonly ISession _session;
        private readonly string _matchId;

        private int _maxPlayers;
        private bool _joinable;
        private string _hostUserId;

        private readonly NakamaMetadata _lobbyMetadata;
        private readonly NakamaChat _chat;

        private bool _disposed;
        private bool _firstSnapshotReceived;
        private TaskCompletionSource<bool> _firstSnapshotTcs;
        private readonly object _snapshotGate = new();

        internal NakamaLobby(ISession session,
            ISocket socket,
            IMatch match,
            int maxPlayers,
            string hostUserId,
            Dictionary<string, string> initialMetadata)
        {
            _session = session;
            _socket = socket;
            _matchId = match.Id;
            _maxPlayers = maxPlayers;
            _joinable = true;
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
            AddPlayer(selfPlayer, isLocal: true);
            if (selfPlayer.isOwner)
                SetOwner(selfPlayer);

            if (match.Presences != null)
            {
                foreach (var p in match.Presences)
                {
                    if (p == null || p.UserId == selfId)
                        continue;
                    if (TryGetPlayerInternal(p.UserId, out _))
                        continue;
                    var isPlayerHost = p.UserId == _hostUserId;
                    var player = new NakamaPlayer(this, p.UserId, p.Username, isPlayerHost, isLocal: false);
                    AddPlayer(player, isLocal: false);
                    if (isPlayerHost)
                        SetOwner(player);
                }
            }
        }

        public override void KickPlayer(IPlayer player)
        {
            if (player == null)
                return;
            if (!isOwner)
            {
                Debug.LogWarning("[NakamaLobby] Only the host can kick players.");
                return;
            }
            SendMatchStateAsync(NakamaOpCodes.Kick, new KickMessage { userId = player.id })
                .Forget("[NakamaLobby] Kick failed");
        }

        public override void SetIsLobbyJoinable(bool isJoinable)
        {
            if (!isOwner)
            {
                Debug.LogWarning("[NakamaLobby] Only the host can change joinability.");
                return;
            }
            if (_joinable == isJoinable)
                return;
            _joinable = isJoinable;
            SendMatchStateAsync(NakamaOpCodes.SetJoinable, new JoinableMessage { joinable = isJoinable })
                .Forget("[NakamaLobby] SetJoinable failed");
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
                if (playerList.Count <= 1)
                    return Task.FromException(new InvalidOperationException(
                        "Joined Nakama lobby has no other presences; the owner is gone."));
                tcs = _firstSnapshotTcs ??= new TaskCompletionSource<bool>();
            }
            SendMatchStateBytesAsync(NakamaOpCodes.RequestSnapshot, null)
                .Forget("[NakamaLobby] Snapshot request failed");
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

        public override void LeaveLobby()
        {
            LeaveAsync().Forget("[NakamaLobby] Leave failed");
        }

        private async Task LeaveAsync()
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
            SendMatchStateAsync(NakamaOpCodes.LobbyMetadataPatch, new LobbyMetadataMessage { metadata = patch })
                .Forget("[NakamaLobby] Lobby metadata patch failed");
        }

        internal void BroadcastLocalPlayerMetadata(Dictionary<string, string> snapshot)
        {
            SendMatchStateAsync(NakamaOpCodes.PlayerMetadataPatch, new PlayerMetadataMessage
            {
                userId = localPlayerInternal?.id ?? _session.UserId,
                metadata = snapshot
            }).Forget("[NakamaLobby] Player metadata patch failed");
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
                    if (TryGetPlayerInternal(presence.UserId, out _))
                        continue;

                    var isPlayerHost = presence.UserId == _hostUserId;
                    var player = new NakamaPlayer(this, presence.UserId, presence.Username, isPlayerHost, isLocal: false);
                    AddPlayer(player, isLocal: false);

                    if (isPlayerHost)
                        SetOwner(player);

                    if (this.isOwner)
                        SendSnapshotAsync().Forget("[NakamaLobby] Snapshot broadcast failed");
                }
            }

            if (evt.Leaves != null)
            {
                bool anyLeaves = false;
                foreach (var presence in evt.Leaves)
                {
                    if (presence == null)
                        continue;
                    if (!RemovePlayer(presence.UserId, out var removed))
                        continue;

                    anyLeaves = true;

                    if (removed.isOwner)
                        HandleHostDisappeared();
                }

                lock (_snapshotGate)
                {
                    if (anyLeaves && !_firstSnapshotReceived && playerList.Count <= 1)
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
                        if (TryGetPlayerInternal(state.UserPresence?.UserId, out var sender))
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
                            SendSnapshotAsync().Forget("[NakamaLobby] Snapshot broadcast failed");
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
            RaiseLobbyDestroyed();
            Dispose();
        }

        private void ApplySnapshot(SnapshotMessage msg)
        {
            SetSnapshotReceived();

            _maxPlayers = msg.maxPlayers > 0 ? msg.maxPlayers : _maxPlayers;
            _joinable = msg.joinable;

            if (!string.IsNullOrEmpty(msg.hostUserId) && msg.hostUserId != _hostUserId)
                ChangeHost(msg.hostUserId);

            if (msg.metadata != null)
                _lobbyMetadata.ReplaceFrom(msg.metadata);

            if (msg.playerMetadata != null)
            {
                foreach (var kvp in msg.playerMetadata)
                {
                    if (!TryGetPlayerInternal(kvp.Key, out var player))
                        continue;
                    player.GetMetadata().ReplaceFrom(kvp.Value);
                    RaisePlayerMetadataUpdated(player);
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
            if (!TryGetPlayerInternal(msg.userId, out var player))
                return;
            player.GetMetadata().ReplaceFrom(msg.metadata);
            RaisePlayerMetadataUpdated(player);
        }

        private void ApplyKick(KickMessage msg)
        {
            if (string.IsNullOrEmpty(msg.userId))
                return;
            if (msg.userId == _session.UserId)
            {
                RaiseLobbyDestroyed();
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

            if (!TryGetPlayerInternal(msg.hostUserId, out _))
                return;

            ChangeHost(msg.hostUserId);

            lock (_snapshotGate)
            {
                if (msg.hostUserId == _session.UserId && !_firstSnapshotReceived)
                {
                    SetSnapshotReceived();
                    SendSnapshotAsync().Forget("[NakamaLobby] Snapshot broadcast failed");
                }
            }
        }

        private void HandleHostDisappeared()
        {
            if (playerList.Count == 0)
            {
                RaiseLobbyDestroyed();
                return;
            }

            string newHostId = null;
            for (int i = 0; i < playerList.Count; i++)
            {
                var pid = playerList[i].id;
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
                SendMatchStateAsync(NakamaOpCodes.HostMigration, new HostMigrationMessage { hostUserId = newHostId })
                    .Forget("[NakamaLobby] Host migration broadcast failed");
                SendSnapshotAsync().Forget("[NakamaLobby] Snapshot broadcast failed");
            }
        }

        private void ChangeHost(string newHostUserId)
        {
            if (_hostUserId == newHostUserId)
                return;

            _hostUserId = newHostUserId;

            NakamaPlayer resolved = null;
            if (!string.IsNullOrEmpty(newHostUserId))
                TryGetPlayerInternal(newHostUserId, out resolved);

            SetOwner(resolved);
        }

        private async Task LeaveQuietlyAsync()
        {
            try { await _socket.LeaveMatchAsync(_matchId); }
            catch { /* ignored */ }
            Dispose();
        }

        private Task SendSnapshotAsync()
        {
            if (!isOwner)
                return Task.CompletedTask;

            var snapshot = new SnapshotMessage
            {
                hostUserId = _hostUserId,
                maxPlayers = _maxPlayers,
                joinable = _joinable,
                metadata = new Dictionary<string, string>(_lobbyMetadata.Snapshot()),
                playerMetadata = new Dictionary<string, Dictionary<string, string>>(),
            };

            for (int i = 0; i < playerList.Count; i++)
            {
                var p = playerList[i];
                snapshot.playerMetadata[p.id] = new Dictionary<string, string>(p.GetMetadata().Snapshot());
            }

            return SendMatchStateAsync(NakamaOpCodes.Snapshot, snapshot);
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
