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

        protected NetworkManager networkManager => _networkManager;

        /// <summary>Point the transport at the new host. Called between network stop and restart.</summary>
        protected abstract void ConfigureTransportForHost(ILobby lobby, IPlayer host);

        /// <summary>Optional provider-specific setup once a lobby is joined.</summary>
        protected virtual void OnJoinedLobby(ILobby lobby) { }

        public override void JoinedLobby(ILobby lobby)
        {
            if (_authenticator != null && _networkManager != null)
                _authenticator.Setup(_networkManager, lobby);
            OnJoinedLobby(lobby);
        }

        private void OnDisable()
        {
            if (_networkManager == null)
                return;

            _networkManager.onServerConnectionState -= OnHostConnectionState;
            _networkManager.onClientConnectionState -= OnClientConnectionState;

            if (_networkManager.isServer)
                _networkManager.StopServer();
            if (_networkManager.isClient)
                _networkManager.StopClient();
        }

        public override void LeftLobby(ILobby lobby)
        {
            if (_ongoingMigration != null) StopCoroutine(_ongoingMigration);
            if (_restartingServerCoroutine != null) StopCoroutine(_restartingServerCoroutine);
            if (_restartingClientCoroutine != null) StopCoroutine(_restartingClientCoroutine);

            if (_networkManager == null)
                return;

            _networkManager.onServerConnectionState -= OnHostConnectionState;
            _networkManager.onClientConnectionState -= OnClientConnectionState;

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

        public override void OnHostChanged(ILobby lobby, IPlayer host, bool isLocalPlayer)
        {
            if (_networkManager == null || lobby == null || host == null)
                return;

            if (_ongoingMigration != null) StopCoroutine(_ongoingMigration);
            if (_restartingServerCoroutine != null) StopCoroutine(_restartingServerCoroutine);
            if (_restartingClientCoroutine != null) StopCoroutine(_restartingClientCoroutine);

            _ongoingMigration = StartCoroutine(ChangeHostCoroutine(lobby, host, isLocalPlayer));
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

        private IEnumerator ChangeHostCoroutine(ILobby lobby, IPlayer host, bool isHost)
        {
            yield return StopNetworkCoroutine();

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
        }

        private IEnumerator RestartServerCoroutine()
        {
            do { yield return null; } while (_networkManager.serverState != ConnectionState.Disconnected);
            _networkManager.StartServer();
        }

        private IEnumerator RestartClientCoroutine()
        {
            do { yield return null; } while (_networkManager.clientState != ConnectionState.Disconnected);
            _networkManager.StartClient();
        }

        private void OnHostConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
                return;

            // A disconnect during our own teardown (scene unload at game start,
            // shutdown) is expected — don't schedule a doomed restart.
            if (!this || !gameObject.activeInHierarchy)
                return;

            if (_restartingServerCoroutine != null)
                StopCoroutine(_restartingServerCoroutine);

            _restartingServerCoroutine = StartCoroutine(RestartServerCoroutine());
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
                return;

            // A disconnect during our own teardown (scene unload at game start,
            // shutdown) is expected — don't schedule a doomed restart.
            if (!this || !gameObject.activeInHierarchy)
                return;

            if (_restartingClientCoroutine != null)
                StopCoroutine(_restartingClientCoroutine);

            _restartingClientCoroutine = StartCoroutine(RestartClientCoroutine());
        }
    }
}
