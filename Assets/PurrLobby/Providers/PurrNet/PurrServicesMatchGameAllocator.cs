using System;
using System.Threading;
using System.Threading.Tasks;
using PurrNet.Transports;
using UnityEngine;
#if PURR_SERVICES_MATCHMAKING
using PurrNet.Services;
#endif

namespace PurrNet.Lobby.PurrNet
{
    public enum PurrServicesMatchTransport { Relay, DirectUdp }

    [Serializable]
    public struct MatchmakingRelayRegion
    {
        public string matchmakingRegion;
        public string relayRegion;
    }

    /// <summary>Loads a service-assigned game and installs admission before connecting.</summary>
    [ProviderDependency("dev.purrnet.services", "PurrServices 1.3+")]
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Matchmaking Game Allocator", fileName = "Matchmaking Game Allocator", order = -199)]
    public class PurrServicesMatchGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] string _gameScene;
        [SerializeField] PurrServicesMatchTransport _transport = PurrServicesMatchTransport.Relay;
        [SerializeField] bool _useP2P = true;
        [Tooltip("Maps queue region IDs to PurrTransport relay regions. Unmapped IDs must match a relay region exactly.")]
        [SerializeField] MatchmakingRelayRegion[] _relayRegions =
        {
            new() { matchmakingRegion = "eu", relayRegion = "eu-central" }
        };
        [Tooltip("Maximum time to start a listener or finish game admission.")]
        [SerializeField, Min(5)] float _connectTimeout = 30;
        [Tooltip("For a direct player host, a publicly reachable address. Relay hosts do not need this.")]
        [SerializeField] string _playerHostAddress;
        [SerializeField] ushort _playerHostPort = 7777;
        CancellationTokenSource _connection;
        NetworkManager _manager;
        GameSession _session;
        int _generation;
#if PURR_SERVICES_MATCHMAKING
        AuthService _auth;
        Action _sessionChanged;
#endif

        public override Task<GameStartResponse> AllocateGame(ILobby lobby) =>
            Task.FromResult(lobby == null
                ? GameStartResponse.Failure("A lobby is required.")
                : GameStartResponse.Success(new ConnectionInfo
                {
                    serverAddress = "PURRLOBBY_GAME_" + lobby.id, hostId = lobby.owner?.id
                }));

        public override Task<GameStartResponse> AllocateGame(MatchResult matchResult)
        {
            if (matchResult.lobby != null && string.IsNullOrEmpty(matchResult.matchId))
                return AllocateGame(matchResult.lobby);
            if (string.IsNullOrEmpty(matchResult.matchId) || string.IsNullOrEmpty(matchResult.playerId)
                || string.IsNullOrEmpty(matchResult.connection.connectionToken))
                return Task.FromResult(GameStartResponse.Failure("The match is missing its admission credentials."));
            var connection = matchResult.connection;
            if (matchResult.isHost && matchResult.requiresHostReady && _transport == PurrServicesMatchTransport.DirectUdp)
            {
                if (string.IsNullOrWhiteSpace(_playerHostAddress) || _playerHostPort == 0)
                    return Task.FromResult(GameStartResponse.Failure("Configure the direct player's public host address and port."));
                connection.serverAddress = _playerHostAddress;
                connection.serverPort = _playerHostPort;
            }
            return Task.FromResult(GameStartResponse.Success(connection));
        }

        public override Task LoadGame(ILobby lobby) => LoadGameScene(_gameScene);

        public override async Task ConnectMatch(MatchResult result, ConnectionInfo connection)
        {
            if (result.lobby != null && string.IsNullOrEmpty(result.matchId))
            {
                await Logout();
                await base.ConnectMatch(result, connection);
                return;
            }
#if PURR_SERVICES_MATCHMAKING
            await Logout();
            var manager = NetworkManager.main;
            RequireOffline(manager);
            GameSession.TryGet(manager.gameObject.scene, out var session);
            if (session && session.isExiting)
                throw new OperationCanceledException("The game session is already leaving.");
            if (!ConfigureTransport(manager, connection, result.isHost))
                throw new InvalidOperationException("The match transport could not be configured.");
            var services = PurrServices.instance;
            if (!services.isAuthenticated || services.playerId != result.playerId)
                throw new InvalidOperationException("The player session changed before the match could start.");
            var matchmaking = services.matchmaking.CaptureSession();
            string sessionToken = services.sessionToken;

            _connection = new CancellationTokenSource();
            var attempt = _connection;
            int generation = _generation;
            _connection.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _connectTimeout)));
            var cancellation = _connection.Token;
            _manager = manager;
            _session = session;
            if (_session)
            {
                _session.ConfigurePlayerSession();
                _session.onExiting += OnGameExiting;
            }
            var admission = GetOrAddComponent<PurrServicesMatchAuthenticator>(manager.gameObject);
            _auth = services.auth;
            _sessionChanged = OnAccountChanged;
            _auth.onLoggedOut += _sessionChanged;
            _auth.onLoggedIn += _sessionChanged;

            try
            {
                if (result.isHost)
                    admission.SetupPlayerHost(manager, result.matchId, result.playerId, connection.connectionToken, matchmaking);
                else
                    admission.SetupClient(manager, result.matchId, result.playerId, connection.connectionToken);
                if (result.isHost)
                {
                    if (!result.requiresHostReady)
                        throw new InvalidOperationException("This player-hosted match has already started; host replacement is not supported.");
                    if (manager.transport is PurrTransport)
                        await PrepareRelayRegionAsync(manager, result.region, _relayRegions, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    // Start the listener first. Starting a host here would send
                    // its own admission request while the match is allocating.
                    manager.StartServer();
                    await WaitForListener(manager, cancellation);
                    await PublishReady(ct => matchmaking.PublishConnectionAsync(result.matchId,
                        new MatchmakingConnection { serverAddress = connection.serverAddress, serverPort = connection.serverPort }, ct), cancellation);
                }
                cancellation.ThrowIfCancellationRequested();
                if (!manager || (_session && _session.isExiting) || services.playerId != result.playerId || services.sessionToken != sessionToken)
                    throw new InvalidOperationException("The player session changed while starting the match.");
                if (result.isHost && manager.transport is UDPTransport localUdp) localUdp.address = "127.0.0.1";
                manager.StartClient();
                await WaitForClient(manager, cancellation);
                // Lifetime after admission belongs to GameSession. A timeout
                // token must not disconnect a healthy game 30 seconds later.
                cancellation.ThrowIfCancellationRequested();
                attempt.CancelAfter(Timeout.Infinite);
            }
            catch
            {
                if (generation == _generation)
                    await Logout();
                throw;
            }
#else
            await Task.CompletedTask;
            throw new InvalidOperationException("Install PurrServices 1.3 or newer to use matchmaking game allocation.");
#endif
        }

        public override Task Logout()
        {
            _generation++;
            if (_session) _session.onExiting -= OnGameExiting;
            _session = null;
#if PURR_SERVICES_MATCHMAKING
            if (_auth != null && _sessionChanged != null)
            {
                _auth.onLoggedOut -= _sessionChanged;
                _auth.onLoggedIn -= _sessionChanged;
            }
            _auth = null;
            _sessionChanged = null;
#endif
            _connection?.Cancel();
            _connection?.Dispose();
            _connection = null;
            if (_manager)
            {
                StopNetwork(_manager);
#if PURR_SERVICES_MATCHMAKING
                if (_manager.authenticator is PurrServicesMatchAuthenticator admission) admission.ResetContext();
#endif
            }
            _manager = null;
            return Task.CompletedTask;
        }

        void OnDisable() { _ = Logout(); }
        void OnGameExiting(GameExitReason reason) { _ = Logout(); }
        void OnAccountChanged()
        {
            // Mark the game as terminal before stopping transport, so a new
            // login cannot trigger GameSession's transient reconnect loop.
            try
            {
                if (_session && !_session.isExiting) _session.LeaveToMenu();
            }
            finally { _ = Logout(); }
        }

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            bool ordinaryLobby = string.IsNullOrEmpty(connection.connectionToken);
            ConfigureMatchTransport(manager, connection, ordinaryLobby ? PurrServicesMatchTransport.Relay : _transport, _useP2P);
#if PURR_SERVICES_MATCHMAKING
            if (ordinaryLobby && manager.authenticator is PurrServicesMatchAuthenticator admission)
            {
                admission.ResetContext();
                manager.authenticator = null;
            }
#endif
            return true;
        }

        public static void ConfigureMatchTransport(NetworkManager manager, ConnectionInfo connection,
            PurrServicesMatchTransport mode, bool useP2P = false)
        {
            RequireOffline(manager);
            if (string.IsNullOrWhiteSpace(connection.serverAddress)) throw new ArgumentException("A server address is required.");
            if (mode == PurrServicesMatchTransport.Relay)
            {
                var relay = manager.transport as PurrTransport ?? GetOrAddComponent<PurrTransport>(manager.gameObject);
                relay.roomName = connection.serverAddress;
                relay.useNat = useP2P;
                manager.transport = relay;
            }
            else
            {
                if (connection.serverPort < 1 || connection.serverPort > ushort.MaxValue)
                    throw new ArgumentException("Direct UDP requires a port between 1 and 65535.");
                var udp = manager.transport as UDPTransport ?? GetOrAddComponent<UDPTransport>(manager.gameObject);
                if (!udp.isSupported) throw new NotSupportedException("Direct UDP is not supported on this platform. Use relay.");
                udp.address = connection.serverAddress;
                udp.serverPort = (ushort)connection.serverPort;
                manager.transport = udp;
            }
        }

        /// <summary>Resolve project queue region names without choosing a different geographic region.</summary>
        public static string ResolveRelayRegion(string matchRegion, MatchmakingRelayRegion[] aliases = null)
        {
            if (string.IsNullOrWhiteSpace(matchRegion))
                throw new ArgumentException("The match has no assigned region.", nameof(matchRegion));
            matchRegion = matchRegion.Trim();
            // This is the service's default queue region. Supplying an explicit
            // alias array (including an empty one) replaces the default mapping.
            if (aliases == null) return matchRegion == "eu" ? "eu-central" : matchRegion;
            string mapped = null;
            foreach (var alias in aliases)
            {
                if (!string.Equals(alias.matchmakingRegion?.Trim(), matchRegion, StringComparison.Ordinal)) continue;
                if (mapped != null) throw new ArgumentException("The matched region has duplicate relay mappings.", nameof(aliases));
                if (string.IsNullOrWhiteSpace(alias.relayRegion))
                    throw new ArgumentException("The matched region has an empty relay mapping.", nameof(aliases));
                mapped = alias.relayRegion.Trim();
            }
            return mapped ?? matchRegion;
        }

        /// <summary>Choose only a relay in the matched region; absence is a startup failure.</summary>
        public static RelayServer SelectRelayServer(string matchRegion, RelayServer[] servers,
            MatchmakingRelayRegion[] aliases = null)
        {
            var relayRegion = ResolveRelayRegion(matchRegion, aliases);
            return SelectResolvedRelayServer(matchRegion, relayRegion, servers);
        }

        static RelayServer SelectResolvedRelayServer(string matchRegion, string relayRegion, RelayServer[] servers)
        {
            if (servers != null)
                foreach (var server in servers)
                    if (server.region == relayRegion && !string.IsNullOrWhiteSpace(server.host)) return server;
            throw new InvalidOperationException($"No relay is available for matched region '{matchRegion}' (relay '{relayRegion}').");
        }

        /// <summary>
        /// Pin the listening relay to the service-selected region before StartServer.
        /// Clients discover that relay by joining the published room.
        /// </summary>
        public static async Task PrepareRelayRegionAsync(NetworkManager manager, string matchRegion,
            MatchmakingRelayRegion[] aliases = null, CancellationToken cancellationToken = default)
        {
            RequireOffline(manager);
            if (manager.transport is not PurrTransport relay)
                throw new ArgumentException("A relay transport must be configured before selecting its region.", nameof(manager));
            // Validate configuration before any HTTP request.
            var relayRegion = ResolveRelayRegion(matchRegion, aliases);
            cancellationToken.ThrowIfCancellationRequested();
            if (relay.region == relayRegion && !string.IsNullOrWhiteSpace(relay.host)) return;
            var lookup = PurrTransportUtils.ActualGetRelayServersAsync(relay.masterServer);
            // Observe immediately: cancellation can race even a completed,
            // faulted request before the normal await below is reached.
            _ = ObserveRelayLookup(lookup);
            using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var deadline = Task.Delay(TimeSpan.FromSeconds(10), deadlineCancellation.Token);
            if (await Task.WhenAny(lookup, deadline) != lookup)
            {
                // The installed lookup API cannot abort its request. Observe
                // eventual faults, but never apply a late result to transport.
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out looking up the relay for the matched region.");
            }
            deadlineCancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            var relays = await lookup;
            cancellationToken.ThrowIfCancellationRequested();
            RequireOffline(manager);
            if (manager.transport != relay)
                throw new OperationCanceledException("The game transport changed during relay lookup.");
            relay.SetServer(SelectResolvedRelayServer(matchRegion, relayRegion, relays.servers));
        }

        static async Task ObserveRelayLookup(Task<Relayers> lookup)
        {
            try { await lookup; }
            catch { /* A cancelled startup must not surface a detached HTTP exception. */ }
        }

        internal static void RequireOffline(NetworkManager manager)
        {
            if (!manager) throw new InvalidOperationException("A NetworkManager is required in the game scene.");
            if (manager.serverState != ConnectionState.Disconnected || manager.clientState != ConnectionState.Disconnected)
                throw new InvalidOperationException("Disable automatic networking; matchmaking starts the game after admission is configured.");
        }

        internal static async Task WaitForListener(NetworkManager manager, CancellationToken cancellation)
        {
            do
            {
                cancellation.ThrowIfCancellationRequested();
                if (!manager) throw new InvalidOperationException("The game scene closed before its listener was ready.");
                if (manager.serverState == ConnectionState.Connected) return;
                if (manager.serverState == ConnectionState.Disconnecting) throw new InvalidOperationException("The server listener stopped.");
                await Task.Yield();
            } while (true);
        }

        internal static async Task WaitForClient(NetworkManager manager, CancellationToken cancellation)
        {
            bool observedConnection = false;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                if (!manager) throw new InvalidOperationException("The game scene closed before admission completed.");
                if (manager.clientState == ConnectionState.Connected && manager.isLocalPlayerReady) return;
                observedConnection |= manager.clientState != ConnectionState.Disconnected;
                if (observedConnection && manager.clientState == ConnectionState.Disconnected)
                    throw new InvalidOperationException("The game server rejected or lost the connection.");
                await Task.Yield();
            } while (true);
        }

        internal static void StopNetwork(NetworkManager manager)
        {
            if (!manager) return;
            if (manager.clientState != ConnectionState.Disconnected) manager.StopClient();
            if (manager.serverState != ConnectionState.Disconnected) manager.StopServer();
        }

#if PURR_SERVICES_MATCHMAKING
        internal static async Task PublishReady(Func<CancellationToken, Task<HttpResult<MatchmakingMatchData>>> publish,
            CancellationToken cancellation)
        {
            for (int attempt = 0; ; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                var response = await publish(cancellation);
                if (response.success && response.data != null && response.data.state == "active") return;
                bool transient = response.statusCode == 0 || response.statusCode == 408
                    || response.statusCode == 429 || response.statusCode >= 500;
                if (attempt >= 2 || !transient)
                    throw new InvalidOperationException("The match could not publish a ready connection.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, response.retryAfterSeconds)), cancellation);
            }
        }
#endif
    }
}
