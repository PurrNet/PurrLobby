using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet.Authentication;
using PurrNet.Packing;
using PurrNet.Transports;

namespace PurrNet.Lobby
{
    public struct LobbyAuthPayload : IPackedAuto
    {
        public string playerId;
        public string lobbyId;
    }

    /// <summary>
    /// Validates that incoming game connections belong to players on the lobby
    /// roster and maps <see cref="Connection"/> ⇄ player id. Shared by all
    /// providers; subclass and override <see cref="ValidateConnection"/> for
    /// provider-specific checks (e.g. verifying the connecting SteamID).
    /// </summary>
    public class LobbyAuthenticator : AuthenticationBehaviour<LobbyAuthPayload>, IProvideConnectionToPlayerID
    {
        private readonly Dictionary<string, Connection> _authedPlayers = new();
        private readonly Dictionary<Connection, string> _authedConnections = new();

        protected ILobby lobby { get; private set; }
        protected NetworkManager manager { get; private set; }

        public void Setup(NetworkManager networkManager, ILobby joinedLobby)
        {
            lobby = joinedLobby;
            manager = networkManager;
            _authedPlayers.Clear();
            _authedConnections.Clear();
        }

        public void OnPlayerLeftLobby(string playerID)
        {
            if (_authedPlayers.TryGetValue(playerID, out var conn))
                manager.CloseConnection(conn);
        }

        /// <summary>
        /// Extra provider-specific validation, run after the roster and lobby-id checks pass.
        /// </summary>
        protected virtual bool ValidateConnection(Connection conn, LobbyAuthPayload payload) => true;

        protected override Task<AuthenticationRequest<LobbyAuthPayload>> GetClientPayload()
        {
            return Task.FromResult(new AuthenticationRequest<LobbyAuthPayload>
            {
                payload = new LobbyAuthPayload
                {
                    playerId = lobby.localPlayer.id,
                    lobbyId = lobby.id
                }
            });
        }

        protected override Task<AuthenticationResponse> ValidateClientPayload(Connection conn, LobbyAuthPayload payload)
        {
            if (lobby.TryGetPlayer(payload.playerId, out _)
                && payload.lobbyId == lobby.id
                && ValidateConnection(conn, payload))
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
