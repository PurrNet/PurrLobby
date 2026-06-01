using System.Collections;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrTransportLobbyConnection : LobbyConnectionProvider
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private PurrTransport _transport;
        [SerializeField] private LobbyAuthenticator _authenticator;

        public override void JoinedLobby(ILobby lobby)
        {
            if (_authenticator != null && _networkManager != null)
                _authenticator.Setup(_networkManager, lobby);
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
            if (_ongoingMigration != null)
                StopCoroutine(_ongoingMigration);
            if (_restartingServerCoroutine != null)
                StopCoroutine(_restartingServerCoroutine);
            if (_restartingClientCoroutine != null)
                StopCoroutine(_restartingClientCoroutine);

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

        IEnumerator StopNetworkCoroutine()
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

        IEnumerator ChangeHostCoroutine(string roomName, bool isHost)
        {
            yield return StopNetworkCoroutine();

            _transport.roomName = roomName;

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

        private Coroutine _ongoingMigration;

        public override void OnHostChanged(ILobby lobby, IPlayer host, bool isLocalPlayer)
        {
            if (_networkManager == null || lobby == null || host == null)
                return;

            if (_ongoingMigration != null)
                StopCoroutine(_ongoingMigration);
            if (_restartingServerCoroutine != null)
                StopCoroutine(_restartingServerCoroutine);
            if (_restartingClientCoroutine != null)
                StopCoroutine(_restartingClientCoroutine);

            var roomName = $"{lobby.id}_{host.id}";
            _ongoingMigration = StartCoroutine(ChangeHostCoroutine(roomName, isLocalPlayer));
        }

        IEnumerator RestartServerCoroutine()
        {
            do
            {
                yield return null;
            } while (_networkManager.serverState != ConnectionState.Disconnected);
            _networkManager.StartServer();
        }

        IEnumerator RestartClientCoroutine()
        {
            do
            {
                yield return null;
            } while (_networkManager.clientState != ConnectionState.Disconnected);
            _networkManager.StartClient();
        }

        private Coroutine _restartingServerCoroutine;
        private Coroutine _restartingClientCoroutine;

        private void OnHostConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                if (_restartingServerCoroutine != null)
                    StopCoroutine(_restartingServerCoroutine);

                if (gameObject.activeInHierarchy)
                    _restartingServerCoroutine = StartCoroutine(RestartServerCoroutine());
            }
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                if (_restartingClientCoroutine != null)
                    StopCoroutine(_restartingClientCoroutine);

                if (gameObject.activeInHierarchy)
                    _restartingClientCoroutine = StartCoroutine(RestartClientCoroutine());
            }
        }

        public override void OnPlayerRegistered(IPlayer player) { }

        public override void OnPlayerUnregistered(IPlayer player)
        {
            _authenticator.OnPlayerLeftLobby(player.id);
        }
    }
}
