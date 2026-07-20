using System;
using System.Collections.Generic;
using PurrNet.Services;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetLobby : LobbyBase<PurrNetPlayer>, IDisposable
    {
        public override string id => _lastData.id;

        public override string joinCode => _lastData.code;

        public override int maxPlayers => _lastData.maxPlayers;

        public override IMetadata lobbyData => _metadata;

        public override bool isLobbyJoinable => _lastData.joinable;

        public override ILobbyChat chat => _chat;

        private readonly LobbyService _service;

        private readonly PurrNetChat _chat;

        private LobbyData _lastData;

        private readonly string _localPlayerId;

        private readonly LobbyConnection _connection;

        private readonly PurrNetMetadata _metadata;

        private bool _disposed;

        public PurrNetLobby(LobbyService service, LobbyData data, string playerToken)
        {
            _localPlayerId = PurrServices.instance.auth.playerId;
            _service = service;
            _lastData = data;
            _connection = _service.Connect(data.id, playerToken);
            _metadata = new PurrNetMetadata(service, data.id, false);
            _chat = new PurrNetChat(_connection, this);
            _connection.onDestroyed += OnLobbyDestroyed;
            _connection.onKicked += OnLobbyDestroyed;
            _connection.onSnapshot += OnLobbySnapshot;
            _connection.onPlayerMetadataUpdated += OnPlayerMetadataUpdated;
            _connection.onMetadataUpdated += OnMetadataUpdated;
        }

        private void OnMetadataUpdated(Dictionary<string, string> metadata)
        {
            _metadata.ReplaceFrom(metadata);
        }

        private void OnPlayerMetadataUpdated(string playerId, Dictionary<string, string> metadata)
        {
            if (!TryGetPlayerInternal(playerId, out var player))
                return;

            player.GetMetadata().ReplaceFrom(metadata);
            RaisePlayerMetadataUpdated(player);
        }

        private void OnLobbySnapshot(LobbySnapshot snapshot)
        {
            _lastData = snapshot.lobby;

            if (snapshot.metadata != null)
                _metadata.ReplaceFrom(snapshot.metadata);

            _removedScratch.Clear();
            for (int i = 0; i < playerList.Count; i++)
            {
                var existing = playerList[i];
                bool found = false;

                for (var j = 0; j < snapshot.players.Count; j++)
                {
                    var p = snapshot.players[j];
                    if (existing.id == p.id)
                    {
                        found = true;
                        if (existing.Update(p))
                        {
                            if (existing.id == _localPlayerId)
                                localPlayerInternal = existing;
                            RaisePlayerUpdated(existing);
                        }
                        ApplyPlayerMetadataFromSnapshot(existing, snapshot);
                        break;
                    }
                }

                if (!found)
                    _removedScratch.Add(existing.id);
            }

            for (int i = 0; i < _removedScratch.Count; i++)
                RemovePlayer(_removedScratch[i], out _);
            _removedScratch.Clear();

            for (var j = 0; j < snapshot.players.Count; j++)
            {
                var p = snapshot.players[j];
                if (TryGetPlayerInternal(p.id, out _))
                    continue;

                var player = new PurrNetPlayer(_service, id);
                player.Update(p);

                ApplyPlayerMetadataFromSnapshot(player, snapshot);

                AddPlayer(player, isLocal: player.id == _localPlayerId);
            }

            var newOwner = TryGetPlayerInternal(_lastData.hostPlayerId, out var host) ? host : null;
            if (newOwner != ownerInternal)
                SetOwner(newOwner);
        }

        private readonly List<string> _removedScratch = new();

        private static void ApplyPlayerMetadataFromSnapshot(PurrNetPlayer player, LobbySnapshot snapshot)
        {
            if (snapshot.playerMetadata == null)
                return;

            if (snapshot.playerMetadata.TryGetValue(player.id, out var meta) && meta != null)
                player.GetMetadata().ReplaceFrom(meta);
        }

        private void OnLobbyDestroyed()
        {
            RaiseLobbyDestroyed();
            Dispose();
        }

        public override void KickPlayer(IPlayer player)
        {
            if (!isOwner)
                return;
            _service.KickAsync(_lastData.id, player.id).Forget("[PurrNetLobby] Kick failed");
        }

        public override void SetIsLobbyJoinable(bool isJoinable)
        {
            if (!isOwner)
                return;
            _lastData.joinable = isJoinable;
            _service.SetJoinableAsync(_lastData.id, isJoinable).Forget("[PurrNetLobby] SetJoinable failed");
        }

        public override void LeaveLobby()
        {
            _service.LeaveAsync(_lastData.id).Forget("[PurrNetLobby] Leave failed");
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _connection.onDestroyed -= OnLobbyDestroyed;
            _connection.onKicked -= OnLobbyDestroyed;
            _connection.onSnapshot -= OnLobbySnapshot;
            _connection.onPlayerMetadataUpdated -= OnPlayerMetadataUpdated;
            _connection.onMetadataUpdated -= OnMetadataUpdated;
            _chat.Dispose();
            _connection.Disconnect();
        }
    }
}
