using System;
using System.Collections.Generic;
using PurrNet.Services;

namespace PurrLobby.PurrNet
{
    public class PurrNetLobby : ILobby, IDisposable
    {
        public string id => _lastData.id;

        public IPlayer localPlayer { get; private set; }

        public IPlayer host { get; private set; }

        public int maxPlayers => _lastData.maxPlayers;

        public IReadOnlyList<IPlayer> players => _players;

        public IMetadata lobbyData => _metadata;

        public ILobbyChat chat => _chat;

        public event Action<IPlayer> onPlayerJoined;

        public event Action<IPlayer> onPlayerLeft;

        public event Action<IPlayer> onPlayerUpdated;

        public event Action<IPlayer> onHostChanged;

        public event Action onLobbyDestroyed;

        private readonly LobbyService _service;

        private readonly PurrNetChat _chat;

        private LobbyData _lastData;

        private readonly string _localPlayerId;

        private readonly LobbyConnection _connection;

        private readonly PurrNetMetadata _metadata;

        private readonly List<PurrNetPlayer> _players = new ();

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
            _connection.onPlayerJoined += OnPlayerJoined;
            _connection.onPlayerLeft += OnPlayerLeft;
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

        private void OnPlayerJoined(LobbyPlayer playerInfo)
        {
            var player = new PurrNetPlayer(_service, id);
            player.Update(_lastData.hostPlayerId, playerInfo);
            _players.Add(player);
            onPlayerJoined?.Invoke(player);

            if (player.id == _localPlayerId)
                localPlayer = player;

            if (player.isHost)
            {
                host = player;
                onHostChanged?.Invoke(player);
            }
        }

        private void OnPlayerLeft(string playerId, string newHostId)
        {
            if (!string.IsNullOrEmpty(newHostId))
            {
                _lastData.hostPlayerId = newHostId;
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].isHost && _players[i].id != newHostId)
                    {
                        _players[i].SetIsHost(false);
                        onPlayerUpdated?.Invoke(_players[i]);
                    }
                    else if (_players[i].id == newHostId)
                    {
                        _players[i].SetIsHost(true);
                        host = _players[i];
                        onHostChanged?.Invoke(_players[i]);
                        onPlayerUpdated?.Invoke(_players[i]);
                    }
                }
            }

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].id == playerId)
                {
                    var removed = _players[i];
                    _players.RemoveAt(i);
                    onPlayerLeft?.Invoke(removed);
                    break;
                }
            }
        }

        private void OnLobbySnapshot(LobbySnapshot snapshot)
        {
            _lastData = snapshot.lobby;

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
                            if (_players[i].isHost)
                                host = _players[i];
                            if (_players[i].id == _localPlayerId)
                                localPlayer = _players[i];
                            _players[i].TriggerOnPlayerUpdated();
                            onPlayerUpdated?.Invoke(_players[i]);
                        }
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
                bool found = false;
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].id == p.id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var player = new PurrNetPlayer(_service, id);
                    player.Update(_lastData.hostPlayerId, p);
                    _players.Add(player);

                    if (player.isHost)
                        host = player;

                    onPlayerJoined?.Invoke(player);

                    if (player.id == _localPlayerId)
                        localPlayer = player;

                    if (player.isHost)
                        onHostChanged?.Invoke(player);
                }
            }
        }

        private void OnLobbyDestroyed()
        {
            onLobbyDestroyed?.Invoke();
        }

        public void KickPlayer(IPlayer player)
        {
            _ = _service.KickAsync(_lastData.id, player.id);
        }

        public void LeaveLobby()
        {
            _ = _service.LeaveAsync(_lastData.id);
            Dispose();
        }

        public void Dispose()
        {
            _connection.onDestroyed -= OnLobbyDestroyed;
            _connection.onKicked -= OnLobbyDestroyed;
            _connection.onSnapshot -= OnLobbySnapshot;
            _connection.onPlayerJoined -= OnPlayerJoined;
            _connection.onPlayerLeft -= OnPlayerLeft;
            _connection.onPlayerMetadataUpdated -= OnPlayerMetadataUpdated;
            _connection.onMetadataUpdated -= OnMetadataUpdated;
            _connection.Disconnect();
        }

        public bool TryGetPlayer(string playerId, out IPlayer player)
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
    }
}
