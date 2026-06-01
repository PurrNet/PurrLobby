using System;
using System.Collections.Generic;
using PurrNet.Services;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetLobby : ILobby, IDisposable
    {
        public string id => _lastData.id;

        public string joinCode => _lastData.code;

        public IPlayer localPlayer { get; private set; }

        public IPlayer owner { get; private set; }

        public int maxPlayers => _lastData.maxPlayers;

        public IReadOnlyList<IPlayer> players => _players;

        public IMetadata lobbyData => _metadata;

        public bool isLobbyJoinable => _lastData.joinable;

        public ILobbyChat chat => _chat;

        public bool isOwner => localPlayer?.isOwner == true;

        public event Action<IPlayer> onPlayerJoined;

        public event Action<IPlayer> onPlayerLeft;

        public event Action<IPlayer> onPlayerUpdated;

        public event Action<IPlayer> onOwnerChanged;

        public event Action onLobbyDestroyed;

        private readonly LobbyService _service;

        private readonly PurrNetChat _chat;

        private LobbyData _lastData;

        private readonly string _localPlayerId;

        private readonly LobbyConnection _connection;

        private readonly PurrNetMetadata _metadata;

        private readonly List<PurrNetPlayer> _players = new ();

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
            _metadata.Update(metadata);
        }

        private void OnPlayerMetadataUpdated(string playerId, Dictionary<string, string> metadata)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].id == playerId)
                {
                    var p = _players[i];
                    var meta = (PurrNetMetadata)p.userData;
                    meta.Update(metadata);
                    p.TriggerOnPlayerMetadataUpdated();
                    onPlayerUpdated?.Invoke(p);
                    break;
                }
            }
        }

        private void OnLobbySnapshot(LobbySnapshot snapshot)
        {
            _lastData = snapshot.lobby;
            var previousOwner = owner;

            if (snapshot.metadata != null)
                _metadata.Update(snapshot.metadata);

            for (int i = 0; i < _players.Count; i++)
            {
                bool found = false;

                for (var j = 0; j < snapshot.players.Count; j++)
                {
                    var p = snapshot.players[j];
                    if (_players[i].id == p.id)
                    {
                        found = true;
                        if (_players[i].Update(_lastData.hostPlayerId, p))
                        {
                            if (_players[i].id == _localPlayerId)
                                localPlayer = _players[i];
                            _players[i].TriggerOnPlayerUpdated();
                            onPlayerUpdated?.Invoke(_players[i]);
                        }
                        ApplyPlayerMetadataFromSnapshot(_players[i], snapshot);
                        break;
                    }
                }

                if (!found)
                {
                    var removed = _players[i];
                    _players.RemoveAt(i);
                    onPlayerLeft?.Invoke(removed);
                    i--;
                }
            }

            for (var j = 0; j < snapshot.players.Count; j++)
            {
                var p = snapshot.players[j];
                if (TryGetPlayer(p.id, out _))
                    continue;

                var player = new PurrNetPlayer(_service, id);
                player.Update(_lastData.hostPlayerId, p);
                _players.Add(player);

                if (player.id == _localPlayerId)
                    localPlayer = player;

                ApplyPlayerMetadataFromSnapshot(player, snapshot);

                onPlayerJoined?.Invoke(player);
            }

            owner = TryGetPlayer(_lastData.hostPlayerId, out var host) ? host : null;
            if (owner != null && owner != previousOwner)
                onOwnerChanged?.Invoke(owner);
        }

        private bool TryGetPlayer(string playerId, out PurrNetPlayer player)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].id == playerId)
                {
                    player = _players[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        private static void ApplyPlayerMetadataFromSnapshot(PurrNetPlayer player, LobbySnapshot snapshot)
        {
            if (snapshot.playerMetadata == null)
                return;

            if (snapshot.playerMetadata.TryGetValue(player.id, out var meta) && meta != null)
                ((PurrNetMetadata)player.userData).Update(meta);
        }

        private void OnLobbyDestroyed()
        {
            onLobbyDestroyed?.Invoke();
            Dispose();
        }

        public void KickPlayer(IPlayer player)
        {
            if (!isOwner)
                return;
            _ = _service.KickAsync(_lastData.id, player.id);
        }

        public void SetIsLobbyJoinable(bool isJoinable)
        {
            if (!isOwner)
                return;
            _lastData.joinable = isJoinable;
            _ = _service.SetJoinableAsync(_lastData.id, isJoinable);
        }

        public void LeaveLobby()
        {
            _ = _service.LeaveAsync(_lastData.id);
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
            _connection.Disconnect();
        }

    }
}
