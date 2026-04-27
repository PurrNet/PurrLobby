using System;
using System.Collections.Generic;
using System.Text;
using JamesFrowen.SimpleWeb;
using Newtonsoft.Json;
using UnityEngine;

namespace PurrNet.Services
{
    public enum LobbyConnectionState
    {
        Disconnected,
        Connecting,
        Authenticating,
        Connected
    }

    public class LobbyConnection
    {
        readonly Uri _uri;
        readonly string _apiKey;
        readonly string _sessionToken;
        readonly string _playerToken;

        SimpleWebClient _client;
        LobbyConnectionState _state;
        RetryTimer _retryTimer;
        bool _intentionalDisconnect;

        public LobbyConnectionState state => _state;
        public LobbySnapshot currentSnapshot { get; private set; }

        public event Action<LobbySnapshot> onSnapshot;
        public event Action<ChatMessage> onChat;
        public event Action onKicked;
        public event Action onDestroyed;
        public event Action onConnected;
        public event Action onDisconnected;
        public event Action<string> onError;

        public event Action<LobbyPlayer> onPlayerJoined;
        public event Action<string, string> onPlayerLeft;
        public event Action<bool> onJoinableChanged;
        public event Action<Dictionary<string, string>> onMetadataUpdated;
        public event Action<string, Dictionary<string, string>> onPlayerMetadataUpdated;

        internal LobbyConnection(
            Uri uri,
            string apiKey,
            string sessionToken,
            string playerToken)
        {
            _uri = uri;
            _apiKey = apiKey;
            _sessionToken = sessionToken;
            _playerToken = playerToken;

            PurrServices.instance.RegisterConnection(this);
            Connect();
        }

        void Connect()
        {
            _state = LobbyConnectionState.Connecting;

            var tcpConfig = new TcpConfig(noDelay: true, sendTimeout: 30000, receiveTimeout: 30000);
            _client = SimpleWebClient.Create(16384, 100, tcpConfig);
            _client.onConnect += HandleConnect;
            _client.onDisconnect += HandleDisconnect;
            _client.onData += HandleData;
            _client.onError += HandleError;

            _client.Connect(_uri);
        }

        void HandleConnect()
        {
            _state = LobbyConnectionState.Authenticating;

            var auth = new WsAuthMessage
            {
                type = "auth",
                apiKey = _apiKey,
                sessionToken = _sessionToken,
                playerToken = _playerToken
            };

            var json = JsonConvert.SerializeObject(auth);
            var bytes = Encoding.UTF8.GetBytes(json);
            _client.Send(new ArraySegment<byte>(bytes));
        }

        void HandleDisconnect()
        {
            _state = LobbyConnectionState.Disconnected;

            onDisconnected?.Invoke();

            if (!_intentionalDisconnect && !_retryTimer.retriesExhausted)
            {
                _retryTimer.Start();
            }
            else if (_retryTimer.retriesExhausted && !_intentionalDisconnect)
            {
                onError?.Invoke("Max reconnection attempts exceeded");
            }
        }

        void HandleData(ArraySegment<byte> data)
        {
            if (data.Array == null)
                return;

            var json = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            var type = WsMessageParser.GetMessageType(json);

            if (type == null)
                return;

            switch (type)
            {
                case "authenticated":
                    var authMsg = WsMessageParser.ParseAuthenticated(json);
                    _state = LobbyConnectionState.Connected;
                    currentSnapshot = authMsg.snapshot;
                    _retryTimer.Reset();
                    onConnected?.Invoke();
                    onSnapshot?.Invoke(authMsg.snapshot);
                    break;

                case "snapshot":
                    var snapMsg = WsMessageParser.ParseSnapshot(json);
                    currentSnapshot = snapMsg.snapshot;
                    onSnapshot?.Invoke(snapMsg.snapshot);
                    break;

                case "player_joined":
                    ApplyPlayerJoined(WsMessageParser.Parse<WsPlayerJoinedMessage>(json));
                    break;

                case "player_left":
                    ApplyPlayerLeft(WsMessageParser.Parse<WsPlayerLeftMessage>(json));
                    break;

                case "joinable_changed":
                    ApplyJoinableChanged(WsMessageParser.Parse<WsJoinableChangedMessage>(json));
                    break;

                case "metadata_updated":
                    ApplyMetadataUpdated(WsMessageParser.Parse<WsMetadataUpdatedMessage>(json));
                    break;

                case "player_metadata_updated":
                    ApplyPlayerMetadataUpdated(WsMessageParser.Parse<WsPlayerMetadataUpdatedMessage>(json));
                    break;

                case "chat":
                    var chatMsg = WsMessageParser.ParseChat(json);
                    var chatSnap = currentSnapshot;
                    if (chatSnap.chat == null)
                        chatSnap.chat = new List<ChatMessage>();
                    chatSnap.chat.Add(chatMsg.message);
                    currentSnapshot = chatSnap;
                    onChat?.Invoke(chatMsg.message);
                    break;

                case "kicked":
                    _intentionalDisconnect = true;
                    onKicked?.Invoke();
                    break;

                case "destroyed":
                    _intentionalDisconnect = true;
                    onDestroyed?.Invoke();
                    break;

                case "error":
                    var errMsg = WsMessageParser.ParseError(json);
                    onError?.Invoke(errMsg.message);
                    break;

                case "ping":
                    var pongJson = JsonConvert.SerializeObject(new WsPingMessage { type = "pong" });
                    var pongBytes = Encoding.UTF8.GetBytes(pongJson);
                    _client.Send(new ArraySegment<byte>(pongBytes));
                    break;

                case "pong":
                    break;
            }
        }

        void ApplyPlayerJoined(WsPlayerJoinedMessage msg)
        {
            var snap = currentSnapshot;
            if (snap.players == null)
                snap.players = new List<LobbyPlayer>();
            snap.players.Add(msg.player);
            snap.lobby.version = msg.version;
            currentSnapshot = snap;
            onPlayerJoined?.Invoke(msg.player);
            onSnapshot?.Invoke(currentSnapshot);
        }

        void ApplyPlayerLeft(WsPlayerLeftMessage msg)
        {
            var snap = currentSnapshot;
            snap.players?.RemoveAll(p => p.id == msg.playerId);
            snap.playerMetadata?.Remove(msg.playerId);
            if (!string.IsNullOrEmpty(msg.newHostPlayerId))
                snap.lobby.hostPlayerId = msg.newHostPlayerId;
            snap.lobby.version = msg.version;
            currentSnapshot = snap;
            onPlayerLeft?.Invoke(msg.playerId, msg.newHostPlayerId);
            onSnapshot?.Invoke(currentSnapshot);
        }

        void ApplyJoinableChanged(WsJoinableChangedMessage msg)
        {
            var snap = currentSnapshot;
            snap.lobby.joinable = msg.joinable;
            snap.lobby.version = msg.version;
            currentSnapshot = snap;
            onJoinableChanged?.Invoke(msg.joinable);
            onSnapshot?.Invoke(currentSnapshot);
        }

        void ApplyMetadataUpdated(WsMetadataUpdatedMessage msg)
        {
            var snap = currentSnapshot;
            snap.metadata = msg.metadata;
            snap.lobby.version = msg.version;
            currentSnapshot = snap;
            onMetadataUpdated?.Invoke(msg.metadata);
            onSnapshot?.Invoke(currentSnapshot);
        }

        void ApplyPlayerMetadataUpdated(WsPlayerMetadataUpdatedMessage msg)
        {
            var snap = currentSnapshot;
            if (snap.playerMetadata == null)
                snap.playerMetadata = new Dictionary<string, Dictionary<string, string>>();
            snap.playerMetadata[msg.playerId] = msg.metadata;
            snap.lobby.version = msg.version;
            currentSnapshot = snap;
            onPlayerMetadataUpdated?.Invoke(msg.playerId, msg.metadata);
            onSnapshot?.Invoke(currentSnapshot);
        }

        void HandleError(Exception ex)
        {
            onError?.Invoke(ex.Message);
        }

        public void SendPing()
        {
            if (_state != LobbyConnectionState.Connected)
                return;

            var json = JsonConvert.SerializeObject(new WsPingMessage { type = "ping" });
            var bytes = Encoding.UTF8.GetBytes(json);
            _client.Send(new ArraySegment<byte>(bytes));
        }

        public void Disconnect()
        {
            _intentionalDisconnect = true;
            _retryTimer.Reset();

            if (_client != null)
            {
                _client.Disconnect();
            }

            _state = LobbyConnectionState.Disconnected;
            PurrServices.instance.UnregisterConnection(this);
        }

        internal void Tick()
        {
            _client?.ProcessMessageQueue();

            if (_retryTimer.isActive && _retryTimer.Tick(Time.unscaledDeltaTime))
            {
                Cleanup();
                Connect();
            }
        }

        void Cleanup()
        {
            if (_client == null)
                return;

            _client.onConnect -= HandleConnect;
            _client.onDisconnect -= HandleDisconnect;
            _client.onData -= HandleData;
            _client.onError -= HandleError;
            _client = null;
        }

        internal void Dispose()
        {
            _intentionalDisconnect = true;
            _retryTimer.Reset();

            if (_client != null)
                _client.Disconnect();

            Cleanup();
        }
    }
}
