#if NAKAMA
using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Authentication;
using PurrNet.Packing;
using PurrNet.Transports;

namespace PurrNet.Lobby.Nakama
{
    public struct NakamaLoginPayload : IPackedAuto
    {
        public string playerId;
        public string lobbyId;
    }

    /// <summary>Validates incoming game connections against the active lobby's player roster.</summary>
    public class NakamaLobbyAuthenticator : AuthenticationBehaviour<NakamaLoginPayload>, IProvideConnectionToPlayerID
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

        protected override Task<AuthenticationRequest<NakamaLoginPayload>> GetClientPayload()
        {
            return Task.FromResult(new AuthenticationRequest<NakamaLoginPayload>
            {
                payload = new NakamaLoginPayload
                {
                    playerId = _lobby.localPlayer.id,
                    lobbyId = _lobby.id,
                }
            });
        }

        protected override Task<AuthenticationResponse> ValidateClientPayload(Connection conn, NakamaLoginPayload payload)
        {
            if (_lobby.TryGetPlayer(payload.playerId, out _) && payload.lobbyId == _lobby.id)
            {
                _authedPlayers[payload.playerId] = conn;
                _authedConnections[conn] = payload.playerId;
                return Task.FromResult(new AuthenticationResponse
                {
                    success = true,
                    cookie = payload.playerId,
                });
            }

            return Task.FromResult(new AuthenticationResponse { success = false });
        }

        protected override void UnAuthenticateClient(Connection conn)
        {
            if (_authedConnections.Remove(conn, out var playerId))
                _authedPlayers.Remove(playerId);
        }

        public bool TryGetConnection(string playerID, out Connection conn) => _authedPlayers.TryGetValue(playerID, out conn);

        public bool TryGetPlayerID(Connection conn, out string playerID) => _authedConnections.TryGetValue(conn, out playerID);
    }
}
#endif
