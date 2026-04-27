using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Services
{
    [AddComponentMenu("PurrNet/Services/PurrServicesGUI")]
    public class PurrServicesGUI : MonoBehaviour
    {
        enum Tab { Auth, Lobbies, ActiveLobby, Chat, Edgegap, Log }

        // --- Window state ---
        bool _open;
        Rect _windowRect = new(20, 20, 620, 520);
        Tab _tab = Tab.Auth;

        // --- Auth fields ---
        string _deviceId;
        string _displayName = "";
        string _authUsername = "";
        string _authPassword = "";

        // --- Lobby create fields ---
        string _createName = "";
        int _createMaxPlayers = 8;
        bool _createPublic;

        // --- Lobby join fields ---
        string _joinCode = "";
        string _quickFilterKey = "";
        string _quickFilterValue = "";

        // --- Active lobby state ---
        string _activeLobbyId;
        string _playerToken;
        LobbyConnection _connection;
        LobbySnapshot _snapshot;
        List<LobbyListEntry> _lobbyList;

        // --- Metadata fields ---
        string _metaKey = "";
        string _metaValue = "";
        string _playerMetaKey = "";
        string _playerMetaValue = "";

        // --- Chat ---
        readonly List<ChatMessage> _chatMessages = new();
        string _chatInput = "";
        Vector2 _chatScroll;

        // --- Log ---
        readonly List<string> _logEntries = new();
        Vector2 _logScroll;

        // --- Scroll positions ---
        Vector2 _contentScroll;

        // --- Edgegap state ---
        string _deployRequestId;
        EdgegapStatusResponse _lastDeployStatus;
        bool _deployPolling;

        // --- Busy guard ---
        bool _busy;

        void Awake()
        {
#if UNITY_EDITOR
            // In editor, hash the project path so clones get their own identity
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath));
            _deviceId = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 32).ToLowerInvariant();
#else
            _deviceId = SystemInfo.deviceUniqueIdentifier;
#endif
        }

        void OnGUI()
        {
            // Toggle button in top-right corner
            if (GUI.Button(new Rect(Screen.width - 110, 10, 100, 28), _open ? "Close Debug" : "Debug UI"))
                _open = !_open;

            if (!_open) return;

            _windowRect = GUILayout.Window(94217, _windowRect, DrawWindow, "PurrServices Debug");
        }

        void DrawWindow(int id)
        {
            // --- Tabs ---
            GUILayout.BeginHorizontal();
            DrawTabButton("Auth", Tab.Auth);
            DrawTabButton("Lobbies", Tab.Lobbies);
            DrawTabButton("Active Lobby", Tab.ActiveLobby);
            DrawTabButton("Chat", Tab.Chat);
            DrawTabButton("Edgegap", Tab.Edgegap);
            DrawTabButton("Log", Tab.Log);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            _contentScroll = GUILayout.BeginScrollView(_contentScroll);

            switch (_tab)
            {
                case Tab.Auth:        DrawAuthTab(); break;
                case Tab.Lobbies:     DrawLobbiesTab(); break;
                case Tab.ActiveLobby: DrawActiveLobbyTab(); break;
                case Tab.Chat:        DrawChatTab(); break;
                case Tab.Edgegap:     DrawEdgegapTab(); break;
                case Tab.Log:         DrawLogTab(); break;
            }

            GUILayout.EndScrollView();

            // --- Status bar ---
            GUILayout.Space(4);
            DrawStatusBar();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void DrawTabButton(string label, Tab tab)
        {
            var prev = GUI.backgroundColor;
            if (_tab == tab) GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(label, GUILayout.Height(24)))
                _tab = tab;
            GUI.backgroundColor = prev;
        }

        // ===================== AUTH TAB =====================

        void DrawAuthTab()
        {
            var svc = PurrServices.instance;
            var auth = svc.auth;

            GUILayout.Label("--- Authentication ---");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Device ID", GUILayout.Width(80));
            _deviceId = GUILayout.TextField(_deviceId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Display Name", GUILayout.Width(80));
            _displayName = GUILayout.TextField(_displayName);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Login") && !_busy)
                RunAsync(LoginAsync());
            if (GUILayout.Button("Logout"))
            {
                auth.Logout();
                Log("Logged out");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("--- Username & Password ---");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Username", GUILayout.Width(80));
            _authUsername = GUILayout.TextField(_authUsername);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Password", GUILayout.Width(80));
            _authPassword = GUILayout.PasswordField(_authPassword, '*');
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Register") && !_busy)
                RunAsync(RegisterAsync());
            if (GUILayout.Button("Login (Password)") && !_busy)
                RunAsync(LoginWithPasswordAsync());
            if (GUILayout.Button("Logout"))
            {
                auth.Logout();
                Log("Logged out");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            if (auth != null)
            {
                GUILayout.Label($"Authenticated: {auth.isAuthenticated}");
                GUILayout.Label($"Player ID: {auth.playerId ?? "—"}");
                GUILayout.Label($"Display Name: {auth.displayName ?? "—"}");
                GUILayout.Label($"Token: {Truncate(auth.sessionToken, 32)}");
            }
        }

        async Task LoginAsync()
        {
            var svc = PurrServices.instance;
            var displayName = string.IsNullOrWhiteSpace(_displayName) ? null : _displayName;
            Log($"Auth.LoginAsync(deviceId={Truncate(_deviceId, 12)}, name={displayName ?? "(null)"})");
            var r = await svc.auth.LoginAsync(_deviceId, displayName);
            if (r.success)
                Log($"Login OK — player={r.playerId}, name={r.displayName}");
            else
                LogError($"Login FAILED — {r.error}");
        }

        async Task RegisterAsync()
        {
            var svc = PurrServices.instance;
            var displayName = string.IsNullOrWhiteSpace(_displayName) ? null : _displayName;
            Log($"Auth.RegisterAsync(user={_authUsername}, name={displayName ?? "(null)"})");
            var r = await svc.auth.RegisterAsync(_authUsername, _authPassword, displayName);
            if (r.success)
                Log($"Register OK — player={r.playerId}, name={r.displayName}");
            else
                LogError($"Register FAILED — {r.error}");
        }

        async Task LoginWithPasswordAsync()
        {
            var svc = PurrServices.instance;
            Log($"Auth.LoginWithPasswordAsync(user={_authUsername})");
            var r = await svc.auth.LoginWithPasswordAsync(_authUsername, _authPassword);
            if (r.success)
                Log($"Login OK — player={r.playerId}, name={r.displayName}");
            else
                LogError($"Login FAILED — {r.error}");
        }

        // ===================== LOBBIES TAB =====================

        void DrawLobbiesTab()
        {
            GUILayout.Label("--- Create Lobby ---");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(80));
            _createName = GUILayout.TextField(_createName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Max Players: {_createMaxPlayers}", GUILayout.Width(120));
            _createMaxPlayers = (int)GUILayout.HorizontalSlider(_createMaxPlayers, 1, 16);
            GUILayout.EndHorizontal();

            _createPublic = GUILayout.Toggle(_createPublic, "Public");

            if (GUILayout.Button("Create") && !_busy)
                RunAsync(CreateLobbyAsync());

            GUILayout.Space(8);
            GUILayout.Label("--- Browse / Join ---");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("List Lobbies") && !_busy)
                RunAsync(ListLobbiesAsync());
            if (GUILayout.Button("Quick Join") && !_busy)
                RunAsync(QuickJoinAsync());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Key", GUILayout.Width(70));
            _quickFilterKey = GUILayout.TextField(_quickFilterKey, GUILayout.Width(80));
            GUILayout.Label("Value", GUILayout.Width(40));
            _quickFilterValue = GUILayout.TextField(_quickFilterValue, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Code", GUILayout.Width(40));
            _joinCode = GUILayout.TextField(_joinCode, GUILayout.Width(100));
            if (GUILayout.Button("Join by Code", GUILayout.Width(100)) && !_busy)
                RunAsync(JoinByCodeAsync());
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Lobby list table
            if (_lobbyList != null)
            {
                GUILayout.Label($"Lobbies ({_lobbyList.Count}):");
                foreach (var l in _lobbyList)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label(Truncate(l.id, 8), GUILayout.Width(70));
                    GUILayout.Label(l.name ?? "—", GUILayout.Width(90));
                    GUILayout.Label(l.joinable ? "Open" : "Closed", GUILayout.Width(60));
                    GUILayout.Label(l.code ?? "—", GUILayout.Width(60));
                    GUILayout.Label($"v{l.version}", GUILayout.Width(30));
                    if (GUILayout.Button("Join", GUILayout.Width(50)) && !_busy)
                        RunAsync(JoinLobbyAsync(l.id));
                    GUILayout.EndHorizontal();
                }
            }
        }

        async Task CreateLobbyAsync()
        {
            var opts = new CreateLobbyOptions
            {
                name = string.IsNullOrWhiteSpace(_createName) ? null : _createName,
                maxPlayers = _createMaxPlayers,
                visibility = _createPublic ? LobbyVisibility.Public : LobbyVisibility.Private
            };

            Log($"Lobbies.CreateAsync(name={opts.name ?? "(auto)"}, max={opts.maxPlayers}, vis={opts.visibility})");
            var r = await PurrServices.instance.lobbies.CreateAsync(opts);

            if (r.success)
            {
                Log($"Create OK — id={Truncate(r.lobby.id, 12)}, code={r.lobby.code}");
                EnterLobby(r.lobby.id, r.playerToken, r.lobby);
            }
            else
            {
                LogError($"Create FAILED — {r.error}");
            }
        }

        async Task ListLobbiesAsync()
        {
            Log("Lobbies.ListAsync()");
            var r = await PurrServices.instance.lobbies.ListAsync();
            if (r.success)
            {
                _lobbyList = r.lobbies;
                Log($"List OK — {r.lobbies.Count} lobbies");
            }
            else
            {
                LogError($"List FAILED — {r.error}");
            }
        }

        async Task JoinLobbyAsync(string lobbyId)
        {
            Log($"Lobbies.JoinAsync({Truncate(lobbyId, 12)})");
            var r = await PurrServices.instance.lobbies.JoinAsync(lobbyId);
            if (r.success)
            {
                Log($"Join OK — lobby={Truncate(r.lobbyId, 12)}");
                EnterLobby(r.lobbyId, r.playerToken);
            }
            else
            {
                LogError($"Join FAILED — {r.error}");
            }
        }

        async Task JoinByCodeAsync()
        {
            Log($"Lobbies.JoinByCodeAsync({_joinCode})");
            var r = await PurrServices.instance.lobbies.JoinByCodeAsync(_joinCode);
            if (r.success)
            {
                Log($"JoinByCode OK — lobby={Truncate(r.lobbyId, 12)}");
                EnterLobby(r.lobbyId, r.playerToken);
            }
            else
            {
                LogError($"JoinByCode FAILED — {r.error}");
            }
        }

        async Task QuickJoinAsync()
        {
            Dictionary<string, string> filter = null;
            if (!string.IsNullOrWhiteSpace(_quickFilterKey))
                filter = new Dictionary<string, string> { { _quickFilterKey, _quickFilterValue } };

            Log($"Lobbies.QuickJoinAsync(filter={(_quickFilterKey != "" ? _quickFilterKey + "=" + _quickFilterValue : "none")})");
            var r = await PurrServices.instance.lobbies.QuickJoinAsync(filter);
            if (r.success)
            {
                Log($"QuickJoin OK — lobby={Truncate(r.lobbyId, 12)}");
                EnterLobby(r.lobbyId, r.playerToken);
            }
            else
            {
                LogError($"QuickJoin FAILED — {r.error}");
            }
        }

        // ===================== ACTIVE LOBBY TAB =====================

        void DrawActiveLobbyTab()
        {
            if (string.IsNullOrEmpty(_activeLobbyId))
            {
                GUILayout.Label("No active lobby. Create or join one in the Lobbies tab.");
                return;
            }

            var lobby = _snapshot.lobby;
            var playerId = PurrServices.instance.playerId;
            var isHost = playerId == lobby.hostPlayerId;

            GUILayout.Label("--- Lobby Info ---");
            GUILayout.Label($"ID: {lobby.id}");
            GUILayout.Label($"Name: {lobby.name ?? "—"}");
            GUILayout.Label($"Code: {lobby.code ?? "—"}");
            GUILayout.Label($"Joinable: {lobby.joinable}  |  Version: {lobby.version}");
            GUILayout.Label($"Host: {lobby.hostPlayerId}  |  Max: {lobby.maxPlayers}");
            GUILayout.Label($"Players: {(_snapshot.players != null ? _snapshot.players.Count : 0)}/{lobby.maxPlayers}");

            GUILayout.Space(4);

            // --- Action buttons ---
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(lobby.joinable ? "Close" : "Open") && !_busy)
                RunAsync(SetJoinableAsync(!lobby.joinable));
            if (GUILayout.Button("Poll") && !_busy)
                RunAsync(PollLobbyAsync());
            if (GUILayout.Button("Leave") && !_busy)
                RunAsync(LeaveLobbyAsync());
            if (isHost && GUILayout.Button("Destroy") && !_busy)
                RunAsync(DestroyLobbyAsync());
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // --- WebSocket ---
            GUILayout.Label("--- WebSocket ---");
            GUILayout.BeginHorizontal();
            var connState = _connection != null ? _connection.state.ToString() : "None";
            GUILayout.Label($"State: {connState}", GUILayout.Width(200));
            if (_connection == null)
            {
                if (GUILayout.Button("Connect") && !_busy)
                    ConnectWebSocket();
            }
            else
            {
                if (GUILayout.Button("Disconnect"))
                    DisconnectWebSocket();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // --- Players ---
            GUILayout.Label("--- Players ---");
            if (_snapshot.players != null)
            {
                foreach (var p in _snapshot.players)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label(Truncate(p.id, 10), GUILayout.Width(90));
                    GUILayout.Label(p.displayName ?? "—", GUILayout.Width(100));
                    GUILayout.Label(FormatTimestamp(p.joinedAt), GUILayout.Width(80));
                    if (isHost && p.id != playerId)
                    {
                        if (GUILayout.Button("Kick", GUILayout.Width(50)) && !_busy)
                            RunAsync(KickPlayerAsync(p.id));
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(4);

            // --- Lobby Metadata ---
            GUILayout.Label("--- Lobby Metadata ---");
            if (_snapshot.metadata != null)
            {
                foreach (var kv in _snapshot.metadata)
                    GUILayout.Label($"  {kv.Key} = {kv.Value}");
            }

            GUILayout.BeginHorizontal();
            _metaKey = GUILayout.TextField(_metaKey, GUILayout.Width(100));
            _metaValue = GUILayout.TextField(_metaValue, GUILayout.Width(100));
            if (GUILayout.Button("Set Metadata", GUILayout.Width(100)) && !_busy)
                RunAsync(SetMetadataAsync());
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // --- Player Metadata ---
            GUILayout.Label("--- Player Metadata ---");
            if (_snapshot.playerMetadata != null && playerId != null &&
                _snapshot.playerMetadata.TryGetValue(playerId, out var myMeta))
            {
                foreach (var kv in myMeta)
                    GUILayout.Label($"  {kv.Key} = {kv.Value}");
            }

            GUILayout.BeginHorizontal();
            _playerMetaKey = GUILayout.TextField(_playerMetaKey, GUILayout.Width(100));
            _playerMetaValue = GUILayout.TextField(_playerMetaValue, GUILayout.Width(100));
            if (GUILayout.Button("Set Player Meta", GUILayout.Width(110)) && !_busy)
                RunAsync(SetPlayerMetadataAsync());
            GUILayout.EndHorizontal();
        }

        async Task SetJoinableAsync(bool joinable)
        {
            Log($"Lobbies.SetJoinableAsync({Truncate(_activeLobbyId, 12)}, {joinable})");
            var r = await PurrServices.instance.lobbies.SetJoinableAsync(_activeLobbyId, joinable);
            if (r.success) Log($"SetJoinable({joinable}) OK"); else LogError($"SetJoinable FAILED — {r.error}");
        }

        async Task PollLobbyAsync()
        {
            Log($"Lobbies.PollAsync({Truncate(_activeLobbyId, 12)})");
            var r = await PurrServices.instance.lobbies.PollAsync(_activeLobbyId);
            if (r.success)
            {
                _snapshot = r.snapshot;
                Log("Poll OK — snapshot updated");
            }
            else
            {
                LogError($"Poll FAILED — {r.error}");
            }
        }

        async Task LeaveLobbyAsync()
        {
            Log($"Lobbies.LeaveAsync({Truncate(_activeLobbyId, 12)})");
            var r = await PurrServices.instance.lobbies.LeaveAsync(_activeLobbyId);
            if (r.success) Log("Leave OK"); else LogError($"Leave FAILED — {r.error}");
            if (r.success) ExitLobby();
        }

        async Task DestroyLobbyAsync()
        {
            Log($"Lobbies.DestroyAsync({Truncate(_activeLobbyId, 12)})");
            var r = await PurrServices.instance.lobbies.DestroyAsync(_activeLobbyId);
            if (r.success) Log("Destroy OK"); else LogError($"Destroy FAILED — {r.error}");
            if (r.success) ExitLobby();
        }

        async Task KickPlayerAsync(string targetId)
        {
            Log($"Lobbies.KickAsync({Truncate(_activeLobbyId, 12)}, {Truncate(targetId, 10)})");
            var r = await PurrServices.instance.lobbies.KickAsync(_activeLobbyId, targetId);
            if (r.success) Log($"Kick OK — {Truncate(targetId, 10)}"); else LogError($"Kick FAILED — {r.error}");
        }

        async Task SetMetadataAsync()
        {
            var meta = new Dictionary<string, string> { { _metaKey, _metaValue } };
            Log($"Lobbies.SetMetadataAsync({_metaKey}={_metaValue})");
            var r = await PurrServices.instance.lobbies.SetMetadataAsync(_activeLobbyId, meta);
            if (r.success) Log("SetMetadata OK"); else LogError($"SetMetadata FAILED — {r.error}");
        }

        async Task SetPlayerMetadataAsync()
        {
            var meta = new Dictionary<string, string> { { _playerMetaKey, _playerMetaValue } };
            Log($"Lobbies.SetPlayerMetadataAsync({_playerMetaKey}={_playerMetaValue})");
            var r = await PurrServices.instance.lobbies.SetPlayerMetadataAsync(_activeLobbyId, meta);
            if (r.success) Log("SetPlayerMeta OK"); else LogError($"SetPlayerMeta FAILED — {r.error}");
        }

        // ===================== EDGEGAP TAB =====================

        void DrawEdgegapTab()
        {
            GUILayout.Label("--- Deploy Server ---");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Deploy") && !_busy)
                RunAsync(EdgegapDeployAsync());
            if (GUILayout.Button("Deploy & Wait") && !_busy)
                RunAsync(EdgegapDeployAndWaitAsync());
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("--- Active Deployment ---");

            if (!string.IsNullOrEmpty(_deployRequestId))
            {
                GUILayout.Label($"Request ID: {_deployRequestId}");
                GUILayout.Label($"Status: {_lastDeployStatus.status ?? "unknown"}");
                GUILayout.Label($"Ready: {_lastDeployStatus.ready}");
                GUILayout.Label($"FQDN: {_lastDeployStatus.fqdn ?? "—"}");
                GUILayout.Label($"Public IP: {_lastDeployStatus.publicIp ?? "—"}");

                if (_lastDeployStatus.ports != null)
                {
                    GUILayout.Space(4);
                    GUILayout.Label("Ports:");
                    foreach (var kv in _lastDeployStatus.ports)
                    {
                        GUILayout.Label($"  {kv.Key}: {kv.Value.external} ({kv.Value.protocol})");
                    }
                }

                if (_lastDeployStatus.error)
                {
                    GUILayout.Label($"Error: {_lastDeployStatus.errorDetail ?? "unknown"}");
                }

                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Status") && !_busy)
                    RunAsync(EdgegapGetStatusAsync());
                if (GUILayout.Button("Stop") && !_busy)
                    RunAsync(EdgegapStopAsync());
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No active deployment. Press Deploy to spin up a server.");
            }
        }

        async Task EdgegapDeployAsync()
        {
            Log("Edgegap.DeployAsync()");
            var r = await PurrServices.instance.edgegap.DeployAsync();
            if (r.success)
            {
                _deployRequestId = r.requestId;
                _lastDeployStatus = default;
                Log($"Deploy OK — requestId={Truncate(r.requestId, 16)}");
            }
            else
            {
                LogError($"Deploy FAILED — {r.error}");
            }
        }

        async Task EdgegapDeployAndWaitAsync()
        {
            Log("Edgegap.DeployAndWaitAsync()");
            _deployPolling = true;

            var r = await PurrServices.instance.edgegap.DeployAndWaitAsync();
            _deployPolling = false;

            if (r.success)
            {
                _deployRequestId = r.deployment.requestId;
                _lastDeployStatus = r.deployment;
                Log($"Deploy+Wait OK — ready at {r.deployment.publicIp}:{GetFirstPort(r.deployment)}");
            }
            else
            {
                if (!string.IsNullOrEmpty(r.deployment.requestId))
                {
                    _deployRequestId = r.deployment.requestId;
                    _lastDeployStatus = r.deployment;
                }
                LogError($"Deploy+Wait FAILED — {r.error}");
            }
        }

        async Task EdgegapGetStatusAsync()
        {
            if (string.IsNullOrEmpty(_deployRequestId)) return;

            Log($"Edgegap.GetStatusAsync({Truncate(_deployRequestId, 16)})");
            var r = await PurrServices.instance.edgegap.GetStatusAsync(_deployRequestId);
            if (r.success)
            {
                _lastDeployStatus = r.deployment;
                Log($"Status OK — {r.deployment.status}, ready={r.deployment.ready}");
            }
            else
            {
                LogError($"Status FAILED — {r.error}");
            }
        }

        async Task EdgegapStopAsync()
        {
            if (string.IsNullOrEmpty(_deployRequestId)) return;

            Log($"Edgegap.StopAsync({Truncate(_deployRequestId, 16)})");
            var r = await PurrServices.instance.edgegap.StopAsync(_deployRequestId);
            if (r.success)
            {
                Log($"Stop OK — {Truncate(r.requestId, 16)}");
                _deployRequestId = null;
                _lastDeployStatus = default;
            }
            else
            {
                LogError($"Stop FAILED — {r.error}");
            }
        }

        static string GetFirstPort(EdgegapStatusResponse status)
        {
            if (status.ports == null) return "—";
            foreach (var kv in status.ports)
                return $"{kv.Value.external}";
            return "—";
        }

        // ===================== CHAT TAB =====================

        void DrawChatTab()
        {
            if (string.IsNullOrEmpty(_activeLobbyId))
            {
                GUILayout.Label("Join a lobby to use chat.");
                return;
            }

            GUILayout.Label($"--- Chat ({_chatMessages.Count} messages) ---");

            _chatScroll = GUILayout.BeginScrollView(_chatScroll, GUILayout.Height(300));
            foreach (var msg in _chatMessages)
            {
                string decoded;
                try
                {
                    decoded = Encoding.UTF8.GetString(Convert.FromBase64String(msg.data));
                }
                catch
                {
                    decoded = $"[base64: {Truncate(msg.data, 20)}]";
                }

                GUILayout.Label($"[{msg.seq}] {msg.playerName ?? msg.playerId}: {decoded}");
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _chatInput = GUILayout.TextField(_chatInput);
            if (GUILayout.Button("Send", GUILayout.Width(60)) && !_busy && !string.IsNullOrEmpty(_chatInput))
                RunAsync(SendChatAsync());
            GUILayout.EndHorizontal();
        }

        async Task SendChatAsync()
        {
            var text = _chatInput;
            _chatInput = "";
            var bytes = Encoding.UTF8.GetBytes(text);
            Log($"Lobbies.SendChatAsync(\"{Truncate(text, 30)}\")");
            var r = await PurrServices.instance.lobbies.SendChatAsync(_activeLobbyId, bytes);
            if (r.success) Log($"Chat sent seq={r.seq}"); else LogError($"Chat FAILED — {r.error}");
        }

        // ===================== LOG TAB =====================

        void DrawLogTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"--- Log ({_logEntries.Count}) ---");
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                _logEntries.Clear();
            GUILayout.EndHorizontal();

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(380));
            foreach (var entry in _logEntries)
                GUILayout.Label(entry);
            GUILayout.EndScrollView();
        }

        // ===================== STATUS BAR =====================

        void DrawStatusBar()
        {
            var svc = PurrServices.instance;
            var connState = _connection != null ? _connection.state.ToString() : "None";

            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"Auth: {(svc.isAuthenticated ? "YES" : "NO")}", GUILayout.Width(70));
            GUILayout.Label($"Player: {Truncate(svc.playerId, 10) ?? "—"}", GUILayout.Width(120));
            GUILayout.Label($"Lobby: {(_activeLobbyId != null ? Truncate(_activeLobbyId, 8) : "—")}", GUILayout.Width(110));
            GUILayout.Label($"WS: {connState}", GUILayout.Width(120));
            GUILayout.EndHorizontal();
        }

        // ===================== LOBBY LIFECYCLE =====================

        void EnterLobby(string lobbyId, string playerToken, LobbyData? initialData = null)
        {
            _activeLobbyId = lobbyId;
            _playerToken = playerToken;
            _chatMessages.Clear();

            PurrServices.instance.activePlayerToken = playerToken;

            if (initialData.HasValue)
            {
                _snapshot = new LobbySnapshot { lobby = initialData.Value };
            }

            _tab = Tab.ActiveLobby;
            Log($"Entered lobby {Truncate(lobbyId, 12)}");

            ConnectWebSocket();
        }

        void ExitLobby()
        {
            DisconnectWebSocket();
            _activeLobbyId = null;
            _playerToken = null;
            _snapshot = default;
            _chatMessages.Clear();
            PurrServices.instance.activePlayerToken = null;
            _tab = Tab.Lobbies;
            Log("Exited lobby");
        }

        void ConnectWebSocket()
        {
            if (_connection != null)
                DisconnectWebSocket();

            var svc = PurrServices.instance;
            var wsUrl = svc.serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
            Log($"WS connecting: {wsUrl}/ws/lobby/{_activeLobbyId}");
            _connection = svc.lobbies.Connect(_activeLobbyId, _playerToken);

            _connection.onConnected += () => Log("WS connected");
            _connection.onDisconnected += () => Log("WS disconnected");
            _connection.onError += err => LogError($"WS error: {err}");

            _connection.onSnapshot += snap =>
            {
                _snapshot = snap;
                Log($"WS snapshot v{snap.lobby.version}, {snap.players?.Count ?? 0} players");
            };

            _connection.onChat += msg =>
            {
                _chatMessages.Add(msg);
                _chatScroll.y = float.MaxValue; // auto-scroll
                Log($"WS chat seq={msg.seq} from {msg.playerName ?? msg.playerId}");
            };

            _connection.onKicked += () =>
            {
                Log("WS: you were kicked!");
                ExitLobby();
            };

            _connection.onDestroyed += () =>
            {
                Log("WS: lobby destroyed!");
                ExitLobby();
            };
        }

        void DisconnectWebSocket()
        {
            if (_connection == null) return;
            _connection.Disconnect();
            _connection = null;
            Log("WS disconnected (manual)");
        }

        // ===================== HELPERS =====================

        async void RunAsync(Task task)
        {
            try
            {
                if (_busy) return;
                _busy = true;
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    LogError($"EXCEPTION: {ex.Message}");
                    Debug.LogException(ex);
                }
                finally
                {
                    _busy = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void Log(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            _logEntries.Add($"[{ts}] {message}");

            // Keep log bounded
            if (_logEntries.Count > 200)
                _logEntries.RemoveAt(0);

            _logScroll.y = float.MaxValue;
        }

        void LogError(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            _logEntries.Add($"[{ts}] {message}");

            if (_logEntries.Count > 200)
                _logEntries.RemoveAt(0);

            _logScroll.y = float.MaxValue;
            Debug.LogError($"[PurrDebug] {message}");
        }

        static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLen ? value : value[..maxLen] + "...";
        }

        static string FormatTimestamp(long unixMs)
        {
            if (unixMs <= 0) return "—";
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            return dt.ToString("HH:mm:ss");
        }

        void OnDestroy()
        {
            DisconnectWebSocket();
        }
    }
}
