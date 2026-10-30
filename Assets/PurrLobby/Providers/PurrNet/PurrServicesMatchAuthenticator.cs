#if PURR_SERVICES_MATCHMAKING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PurrNet.Authentication;
using PurrNet.Packing;
using PurrNet.Services;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public struct MatchAuthPayload : IPackedAuto
    {
        public string matchId;
        public string playerId;
        public string connectionToken;
    }

    /// <summary>
    /// Validates the service-issued, per-player admission credential before a
    /// connection becomes a PurrNet player. Configure before starting transport.
    /// </summary>
    [AddComponentMenu("PurrLobby/PurrServices Match Authenticator")]
    [DisallowMultipleComponent]
    public class PurrServicesMatchAuthenticator : AuthenticationBehaviour<MatchAuthPayload>, IProvideConnectionToPlayerID
    {
        public delegate Task<HttpResult<MatchmakingAdmission>> VerifyAdmission(
            string matchId, string playerId, string connectionToken, CancellationToken cancellationToken);

        readonly Dictionary<Connection, CancellationTokenSource> _pending = new();
        readonly Dictionary<string, CancellationTokenSource> _reservedPlayers = new();
        readonly Dictionary<string, Connection> _players = new();
        readonly Dictionary<Connection, string> _connections = new();
        readonly Dictionary<string, int> _teams = new();
        CancellationTokenSource _lifetime;
        ITransport _transport;
        VerifyAdmission _verify;
        string _matchId;
        string _localPlayerId;
        string _localToken;
        int _generation;

        public void SetupClient(NetworkManager manager, string matchId, string playerId, string connectionToken) =>
            Setup(manager, matchId, playerId, connectionToken, null);

        public void SetupPlayerHost(NetworkManager manager, string matchId, string playerId,
            string connectionToken, IMatchmakingClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            Setup(manager, matchId, playerId, connectionToken, client.VerifyAdmissionAsync);
        }

        public void SetupDedicatedServer(NetworkManager manager, string matchId, MatchmakingServerClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            Setup(manager, matchId, null, null, client.VerifyAdmissionAsync);
        }

        /// <summary>Configure a verifier supplied by a trusted server integration.</summary>
        public void Setup(NetworkManager manager, string matchId, string playerId,
            string connectionToken, VerifyAdmission verify)
        {
            if (!manager || !manager.transport || manager.transport.transport == null)
                throw new ArgumentException("A manager with a configured transport is required.", nameof(manager));
            if (manager.serverState != ConnectionState.Disconnected || manager.clientState != ConnectionState.Disconnected)
                throw new InvalidOperationException("Configure match admission before starting the network.");
            if (!Guid.TryParse(matchId, out _)) throw new ArgumentException("Invalid match ID.", nameof(matchId));
            if ((playerId != null || connectionToken != null) && !ValidPlayerCredential(playerId, connectionToken))
                throw new ArgumentException("A player ID and admission credential are required for a client.");

            ResetContext();
            _lifetime = new CancellationTokenSource();
            _matchId = matchId;
            _localPlayerId = playerId;
            _localToken = connectionToken;
            _verify = verify;
            _transport = manager.transport.transport;
            // The base authenticator only observes already authenticated
            // player departures. Raw disconnects must also fence pending HTTP.
            _transport.onDisconnected += OnDisconnected;
            manager.authenticator = this;
            if (manager.authenticator != this)
                throw new InvalidOperationException("The manager rejected the match authenticator.");
        }

        protected override Task<AuthenticationRequest<MatchAuthPayload>> GetClientPayload()
        {
            if (_lifetime == null || _lifetime.IsCancellationRequested || !ValidPlayerCredential(_localPlayerId, _localToken))
                throw new InvalidOperationException("Match admission is not configured for this client.");
            return Task.FromResult(new AuthenticationRequest<MatchAuthPayload>
            {
                payload = new MatchAuthPayload { matchId = _matchId, playerId = _localPlayerId, connectionToken = _localToken }
            });
        }

        protected override async Task<AuthenticationResponse> ValidateClientPayload(Connection conn, MatchAuthPayload payload)
        {
            if (!conn.isValid || _verify == null || _lifetime == null || _lifetime.IsCancellationRequested
                || payload.matchId != _matchId || !ValidPlayerCredential(payload.playerId, payload.connectionToken)
                || _pending.ContainsKey(conn) || _connections.ContainsKey(conn)
                || _players.ContainsKey(payload.playerId) || _reservedPlayers.ContainsKey(payload.playerId))
                return new AuthenticationResponse { success = false };

            int generation = _generation;
            var verification = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            verification.CancelAfter(TimeSpan.FromSeconds(Math.Max(0.1, timeout - 0.25)));
            _pending.Add(conn, verification);
            _reservedPlayers.Add(payload.playerId, verification);
            try
            {
                var response = await _verify(_matchId, payload.playerId, payload.connectionToken, verification.Token);
                if (verification.IsCancellationRequested || generation != _generation
                    || !_pending.TryGetValue(conn, out var current) || current != verification
                    || !response.success || response.data == null || !response.data.valid
                    || response.data.playerId != payload.playerId || response.data.team is not (0 or 1))
                    return new AuthenticationResponse { success = false };

                _players.Add(payload.playerId, conn);
                _connections.Add(conn, payload.playerId);
                _teams.Add(payload.playerId, response.data.team.Value);
                return new AuthenticationResponse { success = true, cookie = payload.playerId };
            }
            catch
            {
                // Fail closed; HTTP failures and payloads can contain secrets.
                return new AuthenticationResponse { success = false };
            }
            finally
            {
                if (_pending.TryGetValue(conn, out var current) && current == verification)
                    _pending.Remove(conn);
                if (_reservedPlayers.TryGetValue(payload.playerId, out var reserved) && reserved == verification)
                    _reservedPlayers.Remove(payload.playerId);
                verification.Dispose();
            }
        }

        static bool ValidPlayerCredential(string playerId, string credential) =>
            !string.IsNullOrWhiteSpace(playerId) && playerId.Length <= 256
            && !string.IsNullOrWhiteSpace(credential) && credential.Length <= 128;

        void OnDisconnected(Connection conn, DisconnectReason reason, bool asServer)
        {
            if (asServer) UnAuthenticateClient(conn);
        }

        protected override void UnAuthenticateClient(Connection conn)
        {
            if (_pending.TryGetValue(conn, out var pending))
            {
                _pending.Remove(conn);
                pending.Cancel();
            }
            if (!_connections.Remove(conn, out var playerId)) return;
            if (_players.TryGetValue(playerId, out var current) && current.Equals(conn))
            {
                _players.Remove(playerId);
                _teams.Remove(playerId);
            }
        }

        public bool TryGetConnection(string playerID, out Connection conn) => _players.TryGetValue(playerID, out conn);
        public bool TryGetPlayerID(Connection conn, out string playerID) => _connections.TryGetValue(conn, out playerID);
        public bool TryGetTeam(string playerId, out int team) => _teams.TryGetValue(playerId, out team);

        public void ResetContext()
        {
            _generation++;
            if (_transport != null) _transport.onDisconnected -= OnDisconnected;
            _transport = null;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _pending.Clear();
            _reservedPlayers.Clear();
            _players.Clear();
            _connections.Clear();
            _teams.Clear();
            _verify = null;
            _matchId = _localPlayerId = _localToken = null;
        }

        void OnDestroy() => ResetContext();
    }
}
#endif
