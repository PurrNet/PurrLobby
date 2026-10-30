#if PURR_SERVICES_MATCHMAKING
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PurrNet.Services;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    /// <summary>
    /// Game-server entry point for an already claimed match. A process launcher
    /// supplies the server client and claim at runtime; no secret is serialized.
    /// </summary>
    [AddComponentMenu("PurrLobby/PurrServices Dedicated Match Server")]
    [DisallowMultipleComponent]
    public class PurrServicesDedicatedMatchServer : MonoBehaviour
    {
        [SerializeField] NetworkManager _manager;
        [SerializeField, Min(5)] float _startupTimeout = 45;
        [Tooltip("Maps queue region IDs to PurrTransport relay regions when using relay hosting.")]
        [SerializeField] MatchmakingRelayRegion[] _relayRegions =
        {
            new() { matchmakingRegion = "eu", relayRegion = "eu-central" }
        };
        CancellationTokenSource _lifetime;
        MatchmakingServerClient _client;
        string _matchId;
        bool _active;
        int _generation;

        public string matchId => _matchId;
        public bool isActive => _active;

        /// <summary>
        /// Call with the allocator's claim, before NetworkManager starts. The
        /// endpoint is the address clients can reach (which may differ from a
        /// container's bind port). For DirectUdp, bindPort selects that local port.
        /// </summary>
        public async Task StartClaimAsync(MatchmakingServerClient client, MatchmakingClaim claim,
            string workerId, MatchmakingConnection endpoint,
            PurrServicesMatchTransport transport = PurrServicesMatchTransport.DirectUdp,
            ushort bindPort = 0, CancellationToken cancellationToken = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (claim?.match == null || claim.match.hosting != "dedicated" || claim.match.state != "allocating"
                || string.IsNullOrEmpty(claim.claimToken) || !Guid.TryParse(claim.match.id, out _))
                throw new ArgumentException("An unexpired dedicated allocation claim is required.", nameof(claim));
            if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.serverAddress))
                throw new ArgumentException("The server's advertised endpoint is required.", nameof(endpoint));
            if (_lifetime != null) throw new InvalidOperationException("This server is already assigned to a match.");
            if (!_manager) _manager = GetComponent<NetworkManager>();
            PurrServicesMatchGameAllocator.RequireOffline(_manager);

            var connection = new ConnectionInfo { serverAddress = endpoint.serverAddress,
                serverPort = bindPort == 0 ? endpoint.serverPort : bindPort };
            PurrServicesMatchGameAllocator.ConfigureMatchTransport(_manager, connection, transport);
            var admission = _manager.GetComponent<PurrServicesMatchAuthenticator>()
                ?? _manager.gameObject.AddComponent<PurrServicesMatchAuthenticator>();
            admission.SetupDedicatedServer(_manager, claim.match.id, client);
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lifetime.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _startupTimeout)));
            var attempt = _lifetime;
            var cancellation = attempt.Token;
            int generation = ++_generation;
            _client = client;
            _matchId = claim.match.id;

            try
            {
                // Fence a claim that expired during process startup before
                // opening a listener. Refresh during slow relay connections.
                await Renew(client, claim, workerId, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (_manager.transport is PurrTransport)
                {
                    await PurrServicesMatchGameAllocator.PrepareRelayRegionAsync(_manager, claim.match.region, _relayRegions, cancellation);
                    // Lookup can consume most of a short claim's remaining
                    // lease. Fence ownership again before opening a listener.
                    await Renew(client, claim, workerId, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                }
                var renewed = Stopwatch.StartNew();
                _manager.StartServer();
                while (_manager && _manager.serverState != ConnectionState.Connected)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (_manager.serverState == ConnectionState.Disconnecting)
                        throw new InvalidOperationException("The game listener stopped during startup.");
                    if (renewed.Elapsed.TotalSeconds >= 5)
                    {
                        await Renew(client, claim, workerId, cancellation);
                        renewed.Restart();
                    }
                    await Task.Yield();
                }
                if (!_manager) throw new InvalidOperationException("The game server was destroyed during startup.");
                cancellation.ThrowIfCancellationRequested();
                await Renew(client, claim, workerId, cancellation);
                // Renewal and publication are sequential. A renewal racing a
                // successful publish could otherwise reject an active claim.
                await PurrServicesMatchGameAllocator.PublishReady(ct => client.PublishConnectionAsync(claim.match.id, claim.claimToken,
                    new MatchmakingConnection { serverAddress = endpoint.serverAddress, serverPort = endpoint.serverPort }, ct), cancellation);
                cancellation.ThrowIfCancellationRequested();
                _active = true;
                attempt.CancelAfter(Timeout.Infinite);
            }
            catch
            {
                if (generation == _generation) StopLocal();
                // A superseded claimant must never cancel a newer server's
                // match. An external allocator/watchdog decides backend aborts.
                throw;
            }
        }

        static async Task Renew(MatchmakingServerClient client, MatchmakingClaim claim,
            string workerId, CancellationToken cancellation)
        {
            var response = await client.RenewClaimAsync(claim.match.id, workerId, claim.claimToken, cancellation);
            if (!response.success || response.data?.match == null)
                throw new InvalidOperationException("The allocation lease expired or was replaced.");
        }

        /// <summary>Called by authoritative gameplay logic after deciding the outcome.</summary>
        public async Task<HttpResult<MatchmakingResult>> ReportResultAsync(string resultId, int? winningTeam,
            CancellationToken cancellationToken = default)
        {
            if (_client == null || !_active) throw new InvalidOperationException("This server has no active match.");
            int generation = _generation;
            var response = await _client.SubmitResultAsync(_matchId, resultId, winningTeam, cancellationToken);
            // Keep the server running so gameplay can broadcast its game-over
            // screen; the caller then shuts down or returns the process to pool.
            if (response.success && generation == _generation) _active = false;
            return response;
        }

        public async Task<HttpResult<MatchmakingMatchData>> CancelMatchAsync(MatchmakingCancellationReason reason,
            CancellationToken cancellationToken = default)
        {
            if (_client == null || !_active) throw new InvalidOperationException("This server has no active match.");
            int generation = _generation;
            var response = await _client.CancelMatchAsync(_matchId, reason, cancellationToken);
            if (response.success && generation == _generation) StopLocal();
            return response;
        }

        /// <summary>Local shutdown only; does not erase a ranked outcome on the backend.</summary>
        public void StopLocal()
        {
            _generation++;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _active = false;
            PurrServicesMatchGameAllocator.StopNetwork(_manager);
            if (_manager && _manager.authenticator is PurrServicesMatchAuthenticator admission) admission.ResetContext();
            _client = null;
            _matchId = null;
        }

        void OnDestroy() => StopLocal();
    }
}
#endif
