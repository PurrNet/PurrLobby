using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Authentication;
using PurrNet.Packing;
using PurrNet.Transports;

namespace PurrNet.Lobby.PurrNet
{
    public struct LoginPayload : IPackedAuto
    {
        public string playerId;
        public string lobbyId;
    }

    public class LobbyAuthenticator : AuthenticationBehaviour<LoginPayload>, IProvideConnectionToPlayerID
    {
        private readonly Dictionary<string, Connection> _authedPlayers = new();
        private readonly Dictionary<Connection, string> _authedConnections = new();

        private ILobby _lobby;
        private NetworkManager _manager;

        public void Setup(NetworkManager manager, ILobby lobby)
        {
            _lobby = lobby;
            _manager = manager;
            _authedPlayers.Clear();
            _authedConnections.Clear();
        }

        public void OnPlayerLeftLobby(string playerID)
        {
            if (_authedPlayers.TryGetValue(playerID, out var conn))
                _manager.CloseConnection(conn);
        }

        protected override Task<AuthenticationRequest<LoginPayload>> GetClientPayload()
        {
            return Task.FromResult(new AuthenticationRequest<LoginPayload>
            {
                payload = new LoginPayload
                {
                    playerId = _lobby.localPlayer.id,
                    lobbyId = _lobby.id
                }
            });
        }

        private bool LobbyContainsPlayer(string playerId)
        {
            var players = _lobby.players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].id == playerId)
                    return true;
            }
            return false;
        }

        protected override Task<AuthenticationResponse> ValidateClientPayload(Connection conn, LoginPayload payload)
        {
            if (LobbyContainsPlayer(payload.playerId) && payload.lobbyId == _lobby.id)
            {
                _authedPlayers[payload.playerId] = conn;
                _authedConnections[conn] = payload.playerId;
                return Task.FromResult(new AuthenticationResponse
                {
                    success = true,
                    cookie = payload.playerId
                });
            }

            return Task.FromResult(new AuthenticationResponse { success = false });
        }

        protected override void UnAuthenticateClient(Connection conn)
        {
            if (_authedConnections.Remove(conn, out var playerId))
                _authedPlayers.Remove(playerId);
        }

        public bool TryGetConnection(string playerID, out Connection conn)
        {
            return _authedPlayers.TryGetValue(playerID, out conn);
        }

        public bool TryGetPlayerID(Connection conn, out string playerID)
        {
            return _authedConnections.TryGetValue(conn, out playerID);
        }
    }
}
