using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PurrLobby
{
    public sealed class PurrLobbyImgui : MonoBehaviour
    {
        enum MenuState
        {
            Login,
            MainMenu,
            CreateLobby,
            BrowseLobbies,
            JoinByCode,
            InLobby
        }

        [SerializeField] private MenuOrchestrator _orchestrator;
        [SerializeField] private GUISkin skin;

        struct Toast
        {
            public string message;
            public float timer;
        }

        // State machine
        private MenuState _state = MenuState.Login;
        private bool _loading;
        private readonly List<Toast> _toasts = new();

        // Connection state
        private ILobby _lobby;

        // Create lobby fields
        private string _lobbyName = "My Lobby";
        private int _maxPlayers = 4;
        private int _visibilityIndex;

        // Join by code
        private string _joinCode = "";

        // Query results
        private IReadOnlyList<LobbyInfo> _queryResults;

        // Chat
        private string _chatInput = "";
        private readonly List<string> _chatMessages = new();
        private Vector2 _chatScroll;

        // Game starter result
        private ConnectionInfo? _lastConnectionInfo;

        // Layout
        const float BTN_H = 32f;

        // Scroll
        private Vector2 _lobbyListScroll;
        private Vector2 _playerListScroll;

        // Window
        private Rect _windowRect = new Rect(0, 0, 400, 10);

        // Styles (layout-only overrides, no colors)
        private bool _stylesReady;
        private GUISkin _lastSkin;
        private GUIStyle _title, _subtitle, _headerLabel;

        // -----------------------------------------

        private void Start()
        {
            _orchestrator.Initialize();
            /*var session = _orchestrator != null ? _orchestrator.sessionProvider : null;
            if (session != null && session.isLoggedIn)*/
                _state = MenuState.MainMenu;
        }

        private void ShowError(string message)
        {
            _toasts.Add(new Toast { message = message, timer = 4f });
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.timer -= Time.unscaledDeltaTime;
                if (t.timer <= 0f)
                    _toasts.RemoveAt(i);
                else
                    _toasts[i] = t;
            }
        }

        private void SubscribeLobbyEvents(ILobby lobby)
        {
            lobby.onLobbyDestroyed += () =>
            {
                _lobby = null;
                _chatMessages.Clear();
                _lastConnectionInfo = null;
                _state = MenuState.MainMenu;
            };

            if (lobby.chat != null)
            {
                lobby.chat.onMessageReceived += (player, data) =>
                {
                    if (data.Array != null)
                    {
                        var text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
                        _chatMessages.Add($"[{DateTime.Now:HH:mm:ss}] {player.displayName}: {text}");
                    }
                };
            }
        }

        // -----------------------------------------
        // Styles (layout only -- colors come from the skin)
        // -----------------------------------------

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };

            _headerLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        // -----------------------------------------
        // Card width per state
        // -----------------------------------------

        private float GetCardWidth()
        {
            return _state switch
            {
                MenuState.BrowseLobbies => 650,
                MenuState.InLobby => 500,
                _ => 400
            };
        }

        // -----------------------------------------
        // Main render
        // -----------------------------------------

        private void OnGUI()
        {
            if (skin != _lastSkin)
                _stylesReady = false;
            _lastSkin = skin;

            var prevSkin = GUI.skin;
            if (skin != null)
                GUI.skin = skin;

            InitStyles();

            DrawStatusPill();

            _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, GUIContent.none,
                GUI.skin.box, GUILayout.Width(GetCardWidth()));

            // Clamp to screen
            float maxH = Screen.height - 80;
            if (_windowRect.height > maxH)
                _windowRect.height = maxH;

            // Center
            _windowRect.x = (Screen.width - _windowRect.width) / 2f;
            _windowRect.y = (Screen.height - _windowRect.height) / 2f;

            DrawToasts();

            GUI.skin = prevSkin;
        }

        private void DrawWindow(int id)
        {
            switch (_state)
            {
                case MenuState.MainMenu:     DrawMainMenu(); break;
                case MenuState.CreateLobby:  DrawCreateLobby(); break;
                case MenuState.BrowseLobbies:DrawBrowseLobbies(); break;
                case MenuState.JoinByCode:   DrawJoinByCode(); break;
                case MenuState.InLobby:      DrawInLobby(); break;
            }
        }

        // -----------------------------------------
        // Status pill
        // -----------------------------------------

        private void DrawStatusPill()
        {
            /*
            var stateLabel = _state switch
            {
                MenuState.Login => "Login",
                MenuState.MainMenu => "Main Menu",
                MenuState.CreateLobby => "Create Lobby",
                MenuState.BrowseLobbies => "Browse Lobbies",
                MenuState.JoinByCode => "Join by Code",
                MenuState.InLobby => "In Lobby",
                _ => ""
            };

            var session = _orchestrator?.sessionProvider;
            string text = session != null && session.isLoggedIn
                ? $"{session.playerName}  |  {stateLabel}"
                : stateLabel;

            if (_loading)
            {
                int dots = ((int)(Time.unscaledTime * 3f)) % 4;
                text += $"  |  Working{new string('.', dots)}";
            }

            var content = new GUIContent(text);
            var size = GUI.skin.label.CalcSize(content);
            var rect = new Rect(10, 10, size.x + 20, size.y + 8);
            GUI.Box(rect, content);*/
        }

        // -----------------------------------------
        // Error toast
        // -----------------------------------------

        private void DrawToasts()
        {
            if (_toasts.Count == 0) return;

            float tw = 350, th = 40, pad = 16, gap = 4;
            var prev = GUI.color;

            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                int fromBottom = _toasts.Count - 1 - i;
                float alpha = Mathf.Clamp01(_toasts[i].timer / 0.5f);
                GUI.color = new Color(1, 1, 1, alpha);

                float y = Screen.height - pad - (th + gap) * (fromBottom + 1) + gap;
                var rect = new Rect(Screen.width - tw - pad, y, tw, th);
                GUI.Box(rect, _toasts[i].message);
            }

            GUI.color = prev;
        }

        // -----------------------------------------
        // 1. Login
        // -----------------------------------------

        // -----------------------------------------
        // 2. Main Menu
        // -----------------------------------------

        private void DrawMainMenu()
        {
            GUI.enabled = !_loading;

            if (GUILayout.Button("Create Lobby", GUILayout.Height(BTN_H)))
                _state = MenuState.CreateLobby;

            GUILayout.Space(4);

            if (GUILayout.Button("Browse Lobbies", GUILayout.Height(BTN_H)))
            {
                _loading = true;
                _orchestrator.lobbyProvider.QueryLobbies(response =>
                {
                    _loading = false;
                    if (response.success)
                    {
                        _queryResults = response.lobbies;
                        _state = MenuState.BrowseLobbies;
                    }
                    else
                    {
                        ShowError(response.error);
                    }
                });
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Join by Code", GUILayout.Height(BTN_H)))
                _state = MenuState.JoinByCode;

            GUILayout.Space(4);

            if (GUILayout.Button("Quick Play", GUILayout.Height(BTN_H)))
            {
                _loading = true;
                _orchestrator.lobbyProvider.JoinRandom(response =>
                {
                    _loading = false;
                    if (response.success)
                    {
                        _lobby = response.lobby;
                        SubscribeLobbyEvents(_lobby);
                        _state = MenuState.InLobby;
                    }
                    else
                    {
                        ShowError(response.error);
                    }
                });
            }

            GUI.enabled = true;

            GUILayout.Space(16);
            DrawSeparator();
            GUILayout.Space(4);

            if (GUILayout.Button("Disconnect", GUILayout.Height(BTN_H)))
            {
                _lobby = null;
                _lastConnectionInfo = null;
                _chatMessages.Clear();
                _state = MenuState.Login;
            }
        }

        // -----------------------------------------
        // 3. Create Lobby
        // -----------------------------------------

        private void DrawCreateLobby()
        {
            GUILayout.Label("Create Lobby", _title);
            GUILayout.Space(8);

            GUILayout.Label("Lobby Name", _subtitle);
            _lobbyName = GUILayout.TextField(_lobbyName);

            GUILayout.Space(4);

            GUILayout.Label($"Max Players: {_maxPlayers}", _subtitle);
            _maxPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(_maxPlayers, 2, 16));

            GUILayout.Space(4);

            GUILayout.Label("Visibility", _subtitle);
            _visibilityIndex = GUILayout.Toolbar(_visibilityIndex, new[] { "Public", "Private" });

            GUILayout.Space(8);

            GUI.enabled = !_loading;
            if (GUILayout.Button("Create", GUILayout.Height(BTN_H)))
            {
                var s = new LobbySettings
                {
                    name = _lobbyName,
                    maxPlayers = _maxPlayers,
                    visibility = (LobbyVisibility)_visibilityIndex
                };
                _loading = true;
                _orchestrator.lobbyProvider.CreateLobby(s, response =>
                {
                    _loading = false;
                    if (response.success)
                    {
                        _lobby = response.lobby;
                        SubscribeLobbyEvents(_lobby);
                        _chatMessages.Clear();
                        _lastConnectionInfo = null;
                        _state = MenuState.InLobby;
                    }
                    else
                    {
                        ShowError(response.error);
                    }
                });
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            if (GUILayout.Button("Back", GUILayout.Height(BTN_H)))
                _state = MenuState.MainMenu;
        }

        // -----------------------------------------
        // 4. Browse Lobbies
        // -----------------------------------------

        private void DrawBrowseLobbies()
        {
            GUILayout.Label("Browse Lobbies", _title);
            GUILayout.Space(8);

            GUI.enabled = !_loading;
            if (GUILayout.Button("Refresh", GUILayout.Height(BTN_H)))
            {
                _loading = true;
                _orchestrator.lobbyProvider.QueryLobbies(response =>
                {
                    _loading = false;
                    if (response.success)
                        _queryResults = response.lobbies;
                    else
                        ShowError(response.error);
                });
            }
            GUI.enabled = true;

            GUILayout.Space(4);

            if (_queryResults != null && _queryResults.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", _headerLabel, GUILayout.Width(300));
                GUILayout.Label("Players", _headerLabel, GUILayout.Width(80));
                GUILayout.Label("Code", _headerLabel, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                DrawSeparator();

                _lobbyListScroll = GUILayout.BeginScrollView(_lobbyListScroll, GUILayout.MaxHeight(400));

                for (int i = 0; i < _queryResults.Count; i++)
                {
                    var info = _queryResults[i];

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(info.name ?? info.id ?? "", GUILayout.Width(300));
                    GUILayout.Label($"{info.playerCount}/{info.maxPlayers}", GUILayout.Width(80));
                    GUILayout.Label(info.code ?? "-", GUILayout.Width(120));
                    GUILayout.FlexibleSpace();

                    GUI.enabled = !_loading;
                    if (GUILayout.Button("Join", GUILayout.Width(50)))
                    {
                        var lobbyId = info.id;
                        _loading = true;
                        _orchestrator.lobbyProvider.JoinLobby(lobbyId, response =>
                        {
                            _loading = false;
                            if (response.success)
                            {
                                _lobby = response.lobby;
                                SubscribeLobbyEvents(_lobby);
                                _chatMessages.Clear();
                                _lastConnectionInfo = null;
                                _state = MenuState.InLobby;
                            }
                            else
                            {
                                ShowError(response.error);
                            }
                        });
                    }
                    GUI.enabled = true;

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }
            else if (_queryResults != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("No lobbies found.", _subtitle);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Back", GUILayout.Height(BTN_H)))
                _state = MenuState.MainMenu;
        }

        // -----------------------------------------
        // 5. Join by Code
        // -----------------------------------------

        private void DrawJoinByCode()
        {
            GUILayout.Label("Join by Code", _title);
            GUILayout.Space(12);

            GUILayout.Label("Lobby Code", _subtitle);
            _joinCode = GUILayout.TextField(_joinCode);

            GUILayout.Space(8);

            GUI.enabled = !_loading && !string.IsNullOrWhiteSpace(_joinCode);
            if (GUILayout.Button("Join", GUILayout.Height(BTN_H)))
            {
                var code = _joinCode;
                _loading = true;
                _orchestrator.lobbyProvider.JoinLobbyByCode(code, response =>
                {
                    _loading = false;
                    if (response.success)
                    {
                        _lobby = response.lobby;
                        SubscribeLobbyEvents(_lobby);
                        _chatMessages.Clear();
                        _lastConnectionInfo = null;
                        _state = MenuState.InLobby;
                    }
                    else
                    {
                        ShowError(response.error);
                    }
                });
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            if (GUILayout.Button("Back", GUILayout.Height(BTN_H)))
                _state = MenuState.MainMenu;
        }

        // -----------------------------------------
        // 6. In Lobby
        // -----------------------------------------

        private void DrawInLobby()
        {
            if (_lobby == null) { _state = MenuState.MainMenu; return; }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Lobby: {_lobby.id}", _headerLabel);
            if (GUILayout.Button("Copy ID", GUILayout.Width(60), GUILayout.Height(20)))
                GUIUtility.systemCopyBuffer = _lobby.id;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Max Players: {_lobby.maxPlayers}", _subtitle);
            GUILayout.Space(4);

            // Player list
            GUILayout.Label("Players", _headerLabel);
            DrawSeparator();

            var players = _lobby.players;
            if (players != null)
            {
                bool isLocalHost = _lobby.localPlayer is { isHost: true };

                _playerListScroll = GUILayout.BeginScrollView(_playerListScroll, GUILayout.MaxHeight(150));

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.displayName ?? "", GUILayout.Width(200));

                    if (p.isHost)
                        GUILayout.Label("[Host]", _headerLabel, GUILayout.Width(60));
                    else
                        GUILayout.Label("Player", _subtitle, GUILayout.Width(60));

                    GUILayout.FlexibleSpace();

                    bool isSelf = _lobby.localPlayer != null && p.id == _lobby.localPlayer.id;
                    if (isLocalHost && !isSelf)
                    {
                        if (GUILayout.Button("Kick", GUILayout.Width(44), GUILayout.Height(20)))
                        {
                            try { _lobby.KickPlayer(p); }
                            catch (Exception ex) { ShowError(ex.Message); }
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }

            GUILayout.Space(4);
            DrawLobbyChat();
            GUILayout.Space(4);
            DrawGameStarter();

            GUILayout.Space(8);
            DrawSeparator();
            GUILayout.Space(4);

            if (GUILayout.Button("Leave Lobby", GUILayout.Height(BTN_H)))
            {
                try
                {
                    _lobby.LeaveLobby();
                    _lobby = null;
                    _chatMessages.Clear();
                    _lastConnectionInfo = null;
                    _state = MenuState.MainMenu;
                }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void DrawLobbyChat()
        {
            if (_lobby?.chat == null) return;

            GUILayout.Label("Chat", _headerLabel);
            DrawSeparator();

            _chatScroll = GUILayout.BeginScrollView(_chatScroll, GUILayout.Height(100));
            for (int i = 0; i < _chatMessages.Count; i++)
                GUILayout.Label(_chatMessages[i]);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _chatInput = GUILayout.TextField(_chatInput);
            if (GUILayout.Button("Send", GUILayout.Width(52), GUILayout.Height(26)))
            {
                if (!string.IsNullOrEmpty(_chatInput))
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(_chatInput);
                        _lobby.chat.SendMessage(new ArraySegment<byte>(bytes));
                        _chatMessages.Add($"[{DateTime.Now:HH:mm:ss}] (you): {_chatInput}");
                        _chatInput = "";
                    }
                    catch (Exception ex) { ShowError(ex.Message); }
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            var prev = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void DrawGameStarter()
        {
            var gameStarter = _orchestrator?.gameStarterProvider;
            if (!gameStarter) return;

            bool isHost = _lobby.localPlayer is { isHost: true };
            switch (isHost)
            {
                case false when !_lastConnectionInfo.HasValue:
                    return;
                case true:
                {
                    GUI.enabled = !_loading;
                    if (GUILayout.Button("Start Game", GUILayout.Height(BTN_H)))
                    {
                        _loading = true;
                        gameStarter.StartGame(_lobby, response =>
                        {
                            _loading = false;
                            if (response.success)
                                _lastConnectionInfo = response.connection;
                            else
                                ShowError(response.error);
                        });
                    }
                    GUI.enabled = true;
                    break;
                }
            }

            if (_lastConnectionInfo.HasValue)
            {
                var ci = _lastConnectionInfo.Value;
                GUILayout.Space(4);
                GUILayout.Label("Game Ready", _headerLabel);
                GUILayout.Label($"  Address: {ci.serverAddress}:{ci.serverPort}");
                if (!string.IsNullOrEmpty(ci.connectionToken))
                    GUILayout.Label($"  Token: {ci.connectionToken}");
            }
        }
    }
}
