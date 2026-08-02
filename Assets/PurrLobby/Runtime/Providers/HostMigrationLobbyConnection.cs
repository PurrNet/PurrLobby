using System.Collections;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Manages the NetworkManager lifecycle for lobbies whose players host the game,
    /// including host migration: when the lobby owner changes, the network session is
    /// torn down, the transport re-pointed at the new host via
    /// <see cref="ConfigureTransportForHost"/>, and the session restarted.
    /// </summary>
    public abstract class HostMigrationLobbyConnection : LobbyConnectionProvider
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private LobbyAuthenticator _authenticator;

        private Coroutine _ongoingMigration;
        private Coroutine _restartingServerCoroutine;
        private Coroutine _restartingClientCoroutine;
        private ILobby _activeLobby;
        private string _localPlayerId;
        private int _lobbyGeneration;
        private bool _allowNetworkRestart;

        protected NetworkManager networkManager => _networkManager;
        protected bool hasActiveLobbyConnection => _allowNetworkRestart && _activeLobby != null;

        /// <summary>Point the transport at the new host. Called between network stop and restart.</summary>
        protected abstract void ConfigureTransportForHost(ILobby lobby, IPlayer host);

        /// <summary>Optional provider-specific setup once a lobby is joined.</summary>
        protected virtual void OnJoinedLobby(ILobby lobby) { }

        public override void JoinedLobby(ILobby lobby)
        {
            if (lobby == null)
                return;

            if (!ReferenceEquals(_activeLobby, lobby))
            {
                DeactivateLobby(stopNetwork: true);

                _activeLobby = lobby;
                _localPlayerId = lobby.localPlayer?.id;
                _allowNetworkRestart = true;
                _lobbyGeneration++;

                // Subscribe here, rather than relying solely on LobbyView teardown.
                // LobbyBase replays player joins, so this also captures a local player
                // that was not available when JoinedLobby was first called.
                lobby.onPlayerJoined += OnActiveLobbyPlayerJoined;
                lobby.onPlayerLeft += OnActiveLobbyPlayerLeft;
                lobby.onLobbyDestroyed += OnActiveLobbyDestroyed;
            }

            // onLobbyDestroyed has replay semantics. A terminal lobby can therefore
            // deactivate itself synchronously while the subscriptions above are added.
            if (!_allowNetworkRestart || !ReferenceEquals(_activeLobby, lobby))
                return;

            if (_authenticator != null && _networkManager != null)
                _authenticator.Setup(_networkManager, lobby);
            OnJoinedLobby(lobby);
        }

        private void OnDisable()
        {
            DeactivateLobby(stopNetwork: true);
        }

        public override void LeftLobby(ILobby lobby)
        {
            // A stale view must not tear down a newer lobby which happens to use the
            // same connection component.
            if (_activeLobby != null && lobby != null && !ReferenceEquals(_activeLobby, lobby))
                return;

            DeactivateLobby(stopNetwork: true);
        }

        private void DeactivateLobby(bool stopNetwork)
        {
            // Flip the gate before stopping anything. StopClient/StopServer can raise
            // connection-state callbacks synchronously in some transports.
            _allowNetworkRestart = false;
            _lobbyGeneration++;

            var lobby = _activeLobby;
            _activeLobby = null;
            _localPlayerId = null;

            if (lobby != null)
            {
                lobby.onPlayerJoined -= OnActiveLobbyPlayerJoined;
                lobby.onPlayerLeft -= OnActiveLobbyPlayerLeft;
                lobby.onLobbyDestroyed -= OnActiveLobbyDestroyed;
            }

            CancelPendingCoroutines();

            if (_networkManager == null)
                return;

            _networkManager.onServerConnectionState -= OnHostConnectionState;
            _networkManager.onClientConnectionState -= OnClientConnectionState;

            if (!stopNetwork)
                return;

            if (_networkManager.serverState != ConnectionState.Disconnected &&
                _networkManager.serverState != ConnectionState.Disconnecting)
            {
                _networkManager.StopServer();
            }

            if (_networkManager.clientState != ConnectionState.Disconnected &&
                _networkManager.clientState != ConnectionState.Disconnecting)
            {
                _networkManager.StopClient();
            }
        }

        private void CancelPendingCoroutines()
        {
            if (_ongoingMigration != null)
            {
                StopCoroutine(_ongoingMigration);
                _ongoingMigration = null;
            }

            if (_restartingServerCoroutine != null)
            {
                StopCoroutine(_restartingServerCoroutine);
                _restartingServerCoroutine = null;
            }

            if (_restartingClientCoroutine != null)
            {
                StopCoroutine(_restartingClientCoroutine);
                _restartingClientCoroutine = null;
            }
        }

        private void OnActiveLobbyPlayerJoined(IPlayer player)
        {
            var localPlayer = _activeLobby?.localPlayer;
            if (localPlayer != null)
                _localPlayerId = localPlayer.id;
        }

        private void OnActiveLobbyPlayerLeft(IPlayer player)
        {
            if (player == null || string.IsNullOrEmpty(_localPlayerId) || player.id != _localPlayerId)
                return;

            // Losing our own membership is terminal (kick/removal), even when a
            // provider reports the roster update before its destroyed/kicked event.
            DeactivateLobby(stopNetwork: true);
        }

        private void OnActiveLobbyDestroyed()
        {
            DeactivateLobby(stopNetwork: true);
        }

        public override void OnHostChanged(ILobby lobby, IPlayer host, bool isLocalPlayer)
        {
            if (_networkManager == null || lobby == null || host == null ||
                !_allowNetworkRestart || !ReferenceEquals(_activeLobby, lobby))
                return;

            CancelPendingCoroutines();

            int generation = _lobbyGeneration;
            _ongoingMigration = StartCoroutine(ChangeHostCoroutine(lobby, host, isLocalPlayer, generation));
        }

        public override void OnPlayerRegistered(IPlayer player) { }

        public override void OnPlayerUnregistered(IPlayer player)
        {
            if (_authenticator)
                _authenticator.OnPlayerLeftLobby(player.id);
        }

        private IEnumerator StopNetworkCoroutine()
        {
            _networkManager.onServerConnectionState -= OnHostConnectionState;
            _networkManager.onClientConnectionState -= OnClientConnectionState;

            _networkManager.StopServer();
            while (_networkManager.isServer)
                yield return null;

            _networkManager.StopClient();
            while (_networkManager.isClient)
                yield return null;
        }

        private IEnumerator ChangeHostCoroutine(ILobby lobby, IPlayer host, bool isHost, int generation)
        {
            yield return StopNetworkCoroutine();

            if (!CanRestartNetwork(generation))
            {
                _ongoingMigration = null;
                yield break;
            }

            ConfigureTransportForHost(lobby, host);

            if (isHost)
            {
                _networkManager.onServerConnectionState += OnHostConnectionState;
                _networkManager.onClientConnectionState += OnClientConnectionState;
                _networkManager.StartHost();
            }
            else
            {
                _networkManager.onClientConnectionState += OnClientConnectionState;
                _networkManager.StartClient();
            }

            _ongoingMigration = null;
        }

        private IEnumerator RestartServerCoroutine(int generation)
        {
            do { yield return null; } while (_networkManager.serverState != ConnectionState.Disconnected);

            if (!CanRestartNetwork(generation))
            {
                _restartingServerCoroutine = null;
                yield break;
            }

            _restartingServerCoroutine = null;
            _networkManager.StartServer();
        }

        private IEnumerator RestartClientCoroutine(int generation)
        {
            do { yield return null; } while (_networkManager.clientState != ConnectionState.Disconnected);

            if (!CanRestartNetwork(generation))
            {
                _restartingClientCoroutine = null;
                yield break;
            }

            _restartingClientCoroutine = null;
            _networkManager.StartClient();
        }

        private bool CanRestartNetwork(int generation)
        {
            return _allowNetworkRestart && _activeLobby != null && generation == _lobbyGeneration &&
                   this && gameObject.activeInHierarchy;
        }

        private void OnHostConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
                return;

            // A disconnect during our own teardown (scene unload at game start,
            // shutdown) is expected — don't schedule a doomed restart.
            if (!CanRestartNetwork(_lobbyGeneration))
                return;

            if (_restartingServerCoroutine != null)
                StopCoroutine(_restartingServerCoroutine);

            _restartingServerCoroutine = StartCoroutine(RestartServerCoroutine(_lobbyGeneration));
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
                return;

            // A disconnect during our own teardown (scene unload at game start,
            // shutdown) is expected — don't schedule a doomed restart.
            if (!CanRestartNetwork(_lobbyGeneration))
                return;

            if (_restartingClientCoroutine != null)
                StopCoroutine(_restartingClientCoroutine);

            _restartingClientCoroutine = StartCoroutine(RestartClientCoroutine(_lobbyGeneration));
        }
    }
}
