#if NAKAMA
using System.Collections;
using System.Reflection;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Drives the in-game NetworkManager lifecycle when joining/leaving a Nakama lobby and during host
    /// migration. Equivalent to <c>PurrTransportLobbyConnection</c> but transport-agnostic — host
    /// migration sets a configurable string field on whatever transport is wired.
    /// </summary>
    public class NakamaLobbyConnection : LobbyConnectionProvider
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NakamaLobbyAuthenticator _authenticator;

        [Tooltip("Field/property name on the active transport that receives the new room id during host migration.")]
        [SerializeField] private string _transportRoomField = "roomName";

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

        private IEnumerator ChangeHostCoroutine(string roomName, bool isHost)
        {
            yield return StopNetworkCoroutine();

            ApplyRoomField(roomName);

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
            if (_ongoingMigration != null) StopCoroutine(_ongoingMigration);
            if (_restartingServerCoroutine != null) StopCoroutine(_restartingServerCoroutine);
            if (_restartingClientCoroutine != null) StopCoroutine(_restartingClientCoroutine);

            var roomName = $"{lobby.id}_{host.id}";
            _ongoingMigration = StartCoroutine(ChangeHostCoroutine(roomName, isLocalPlayer));
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

        private Coroutine _restartingServerCoroutine;
        private Coroutine _restartingClientCoroutine;

        private void OnHostConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected) return;
            if (_restartingServerCoroutine != null) StopCoroutine(_restartingServerCoroutine);
            if (gameObject.activeInHierarchy)
                _restartingServerCoroutine = StartCoroutine(RestartServerCoroutine());
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected) return;
            if (_restartingClientCoroutine != null) StopCoroutine(_restartingClientCoroutine);
            if (gameObject.activeInHierarchy)
                _restartingClientCoroutine = StartCoroutine(RestartClientCoroutine());
        }

        public override void OnPlayerRegistered(IPlayer player) { }

        public override void OnPlayerUnregistered(IPlayer player)
        {
            if (_authenticator != null)
                _authenticator.OnPlayerLeftLobby(player.id);
        }

        private void ApplyRoomField(string value)
        {
            if (_networkManager == null || _networkManager.transport == null || string.IsNullOrEmpty(_transportRoomField))
                return;

            var transport = _networkManager.transport;
            var type = transport.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var prop = type.GetProperty(_transportRoomField, flags);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(transport, value);
                return;
            }

            var field = type.GetField(_transportRoomField, flags);
            if (field != null && field.FieldType == typeof(string))
                field.SetValue(transport, value);
        }
    }
}
#endif
