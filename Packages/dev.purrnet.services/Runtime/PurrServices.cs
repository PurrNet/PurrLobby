using System.Collections.Generic;
using UnityEngine;

namespace PurrNet.Services
{
    [AddComponentMenu("PurrNet/PurrServices")]
    [DefaultExecutionOrder(-100)]
    public class PurrServices : MonoBehaviour
    {
        [SerializeField] string _serverUrl = "https://purrnet.dev";
        [SerializeField] string _apiKey;

        static PurrServices _instance;

        public static PurrServices instance
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = FindAnyObjectByType<PurrServices>();

                if (_instance)
                    return _instance;

                throw new System.Exception("No `PurrServices` instance found in the scene.");
            }
        }

        ServiceHttp _http;
        AuthService _auth;
        LobbyService _lobbies;
        EdgegapService _edgegap;

        readonly List<LobbyConnection> _connections = new();

        public AuthService auth => _auth;
        public LobbyService lobbies => _lobbies;
        public EdgegapService edgegap => _edgegap;
        public bool isAuthenticated => _auth != null && _auth.isAuthenticated;
        public string sessionToken => _auth?.sessionToken;
        public string playerId => _auth?.playerId;
        public string playerName => _auth?.displayName;
        public string serverUrl => _serverUrl;

        string _activePlayerToken;

        public string activePlayerToken
        {
            get => _activePlayerToken;
            set => _activePlayerToken = value;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeServices();
        }

        void InitializeServices()
        {
            _http = new ServiceHttp(
                () => _serverUrl,
                () => _apiKey,
                () => _auth?.sessionToken,
                () => _activePlayerToken
            );

            _auth = new AuthService(_http);

            _lobbies = new LobbyService(
                _http,
                () => _apiKey,
                () => _auth?.sessionToken,
                () => _serverUrl
            );

            _edgegap = new EdgegapService(_http);
        }

        void Update()
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                _connections[i].Tick();
            }
        }

        void OnDestroy()
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                _connections[i].Dispose();
            }

            _connections.Clear();

            if (_instance == this)
                _instance = null;
        }

        internal void DisconnectAllConnections()
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                _connections[i].Disconnect();
            }
        }

        internal void RegisterConnection(LobbyConnection connection)
        {
            if (!_connections.Contains(connection))
                _connections.Add(connection);
        }

        internal void UnregisterConnection(LobbyConnection connection)
        {
            _connections.Remove(connection);
        }
    }
}
