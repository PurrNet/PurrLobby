using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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

        [SerializeField] private PurrLobbySettings settings;

        // State machine
        private MenuState _state = MenuState.Login;
        private bool _loading;
        private string _errorMessage;
        private float _errorTimer;

        // Connection state
        private string _playerId = Guid.NewGuid().ToString("N")[..8];
        private string _playerName = Environment.MachineName;
        private ILobbyProvider _provider;
        private IGameStarter _gameStarter;
        private ILobby _lobby;

        // Create lobby fields
        private string _lobbyName = "My Lobby";
        private int _maxPlayers = 4;
        private int _visibilityIndex;

        // Join by code
        private string _joinCode = "";

        // Query results
        private IReadOnlyList<LobbyInfo> _queryResults;

        // Metadata fields
        private string _lobbyMetaKey = "";
        private string _lobbyMetaValue = "";
        private string _playerMetaKey = "";
        private string _playerMetaValue = "";

        // Chat
        private string _chatInput = "";
        private readonly List<string> _chatMessages = new();
        private Vector2 _chatScroll;

        // Game starter result
        private ConnectionInfo? _lastConnectionInfo;

        // Layout
        const float CARD_PAD = 20f;
        const float BTN_H = 32f;

        // Styles
        private bool _stylesReady;
        private GUIStyle _box, _title, _subtitle, _btn, _field, _pill, _pillLabel;
        private GUIStyle _errorBox, _errorLabel, _headerLabel, _rowEven, _rowOdd;
        private Texture2D _cardTex, _pillTex, _errorTex, _rowEvenTex, _rowOddTex;

        // ─────────────────────────────────────────

        private void ShowError(string message)
        {
            _errorMessage = message;
            _errorTimer = 4f;
        }

        private void Update()
        {
            if (_errorTimer > 0f)
            {
                _errorTimer -= Time.unscaledDeltaTime;
                if (_errorTimer <= 0f)
                    _errorMessage = null;
            }
        }

        private async void RunAsync(Func<Task> action, MenuState? successState = null)
        {
            if (_loading) return;
            _loading = true;
            try
            {
                await action();
                if (successState.HasValue)
                    _state = successState.Value;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _loading = false;
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

        // ─────────────────────────────────────────
        // Styles
        // ─────────────────────────────────────────

        private static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var pixels = new[] { col, col, col, col };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _cardTex = MakeTex(new Color(0.14f, 0.14f, 0.18f, 0.95f));
            _pillTex = MakeTex(new Color(0.12f, 0.12f, 0.15f, 0.88f));
            _errorTex = MakeTex(new Color(0.55f, 0.08f, 0.08f, 0.92f));
            _rowEvenTex = MakeTex(new Color(0.18f, 0.18f, 0.22f, 0.7f));
            _rowOddTex = MakeTex(new Color(0.15f, 0.15f, 0.19f, 0.7f));

            _box = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _cardTex },
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            _headerLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            _btn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = BTN_H,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white }
            };

            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                fixedHeight = 26
            };

            _pill = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _pillTex },
                padding = new RectOffset(10, 10, 4, 4)
            };

            _pillLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _errorBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _errorTex },
                padding = new RectOffset(12, 12, 6, 6)
            };

            _errorLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.45f, 0.45f) }
            };

            _rowEven = new GUIStyle { normal = { background = _rowEvenTex }, padding = new RectOffset(6, 6, 3, 3) };
            _rowOdd = new GUIStyle { normal = { background = _rowOddTex }, padding = new RectOffset(6, 6, 3, 3) };
        }

        // ─────────────────────────────────────────
        // Main render
        // ─────────────────────────────────────────

        private void OnGUI()
        {
            InitStyles();

            DrawStatusPill();

            GetCardSize(out float cw, out float ch);
            var card = new Rect((Screen.width - cw) / 2f, (Screen.height - ch) / 2f, cw, ch);
            GUI.Box(card, "", _box);

            var inner = new Rect(card.x + CARD_PAD, card.y + CARD_PAD,
                                 card.width - CARD_PAD * 2, card.height - CARD_PAD * 2);
            GUILayout.BeginArea(inner);

            switch (_state)
            {
                case MenuState.Login:        DrawLogin(); break;
                case MenuState.MainMenu:     DrawMainMenu(); break;
                case MenuState.CreateLobby:  DrawCreateLobby(); break;
                case MenuState.BrowseLobbies:DrawBrowseLobbies(); break;
                case MenuState.JoinByCode:   DrawJoinByCode(); break;
                case MenuState.InLobby:      DrawInLobby(); break;
            }

            GUILayout.EndArea();

            DrawErrorToast();
        }

        // ─────────────────────────────────────────
        // Status pill
        // ─────────────────────────────────────────

        private void DrawStatusPill()
        {
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

            string text = _provider != null ? $"{_playerName}  |  {stateLabel}" : stateLabel;
            if (_loading)
            {
                int dots = ((int)(Time.unscaledTime * 3f)) % 4;
                text += $"  |  Working{new string('.', dots)}";
            }

            var content = new GUIContent(text);
            var size = _pillLabel.CalcSize(content);
            var rect = new Rect(10, 10, size.x + 20, size.y + 8);
            GUI.Box(rect, "", _pill);
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, size.x, size.y), content, _pillLabel);
        }

        // ─────────────────────────────────────────
        // Error toast
        // ─────────────────────────────────────────

        private void DrawErrorToast()
        {
            if (string.IsNullOrEmpty(_errorMessage)) return;

            float alpha = Mathf.Clamp01(_errorTimer / 0.5f);
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);

            float tw = 350, th = 40, pad = 16;
            var rect = new Rect(Screen.width - tw - pad, Screen.height - th - pad, tw, th);
            GUI.Box(rect, "", _errorBox);
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, rect.width - 24, rect.height - 12), _errorMessage, _errorLabel);

            GUI.color = prev;
        }

        // ─────────────────────────────────────────
        // Card sizes
        // ─────────────────────────────────────────

        private void GetCardSize(out float width, out float height)
        {
            switch (_state)
            {
                case MenuState.Login:         width = 400; height = 280; break;
                case MenuState.MainMenu:      width = 400; height = 360; break;
                case MenuState.CreateLobby:   width = 400; height = 320; break;
                case MenuState.BrowseLobbies: width = 550; height = 420; break;
                case MenuState.JoinByCode:    width = 400; height = 260; break;
                case MenuState.InLobby:       width = 550; height = 520; break;
                default:                      width = 400; height = 300; break;
            }
        }

        // ─────────────────────────────────────────
        // 1. Login
        // ─────────────────────────────────────────

        private void DrawLogin()
        {
            GUILayout.Label("PurrLobby", _title);
            GUILayout.Space(12);

            GUILayout.Label("Player Name", _subtitle);
            _playerName = GUILayout.TextField(_playerName, _field);

            GUILayout.Space(4);

            GUILayout.Label("Player ID", _subtitle);
            _playerId = GUILayout.TextField(_playerId, _field);

            GUILayout.Space(8);

            if (settings != null)
            {
                GUILayout.Label($"API: {settings.apiUrl}", _subtitle);
                GUILayout.Label($"Game: {settings.gameId}", _subtitle);
            }
            else
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label("No PurrLobbySettings assigned!", _subtitle);
                GUI.color = c;
            }

            GUILayout.Space(8);

            GUI.enabled = !_loading && settings != null;
            if (GUILayout.Button("Connect", _btn))
            {
                _provider = settings.CreateProvider(_playerId, _playerName);
                _gameStarter = settings.CreateGameStarter(_playerId, _playerName);
                _state = MenuState.MainMenu;
            }
            GUI.enabled = true;
        }

        // ─────────────────────────────────────────
        // 2. Main Menu
        // ─────────────────────────────────────────

        private void DrawMainMenu()
        {
            GUILayout.Label($"Welcome, {_playerName}!", _title);
            GUILayout.Space(12);

            GUI.enabled = !_loading;

            if (GUILayout.Button("Create Lobby", _btn))
                _state = MenuState.CreateLobby;

            GUILayout.Space(4);

            if (GUILayout.Button("Browse Lobbies", _btn))
            {
                RunAsync(async () =>
                {
                    _queryResults = await _provider.QueryLobbies();
                }, MenuState.BrowseLobbies);
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Join by Code", _btn))
                _state = MenuState.JoinByCode;

            GUILayout.Space(4);

            if (GUILayout.Button("Quick Play", _btn))
            {
                RunAsync(async () =>
                {
                    _lobby = await _provider.JoinRandom();
                    SubscribeLobbyEvents(_lobby);
                }, MenuState.InLobby);
            }

            GUI.enabled = true;

            GUILayout.Space(16);
            DrawSeparator();
            GUILayout.Space(4);

            if (GUILayout.Button("Disconnect", _btn))
            {
                _provider = null;
                _gameStarter = null;
                _lobby = null;
                _lastConnectionInfo = null;
                _chatMessages.Clear();
                _state = MenuState.Login;
            }
        }

        // ─────────────────────────────────────────
        // 3. Create Lobby
        // ─────────────────────────────────────────

        private void DrawCreateLobby()
        {
            GUILayout.Label("Create Lobby", _title);
            GUILayout.Space(8);

            GUILayout.Label("Lobby Name", _subtitle);
            _lobbyName = GUILayout.TextField(_lobbyName, _field);

            GUILayout.Space(4);

            GUILayout.Label($"Max Players: {_maxPlayers}", _subtitle);
            _maxPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(_maxPlayers, 2, 16));

            GUILayout.Space(4);

            GUILayout.Label("Visibility", _subtitle);
            _visibilityIndex = GUILayout.Toolbar(_visibilityIndex, new[] { "Public", "Private" });

            GUILayout.Space(8);

            GUI.enabled = !_loading;
            if (GUILayout.Button("Create", _btn))
            {
                var s = new LobbySettings
                {
                    name = _lobbyName,
                    maxPlayers = _maxPlayers,
                    visibility = (LobbyVisibility)_visibilityIndex
                };
                RunAsync(async () =>
                {
                    _lobby = await _provider.CreateLobby(s);
                    SubscribeLobbyEvents(_lobby);
                    _chatMessages.Clear();
                    _lastConnectionInfo = null;
                }, MenuState.InLobby);
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            if (GUILayout.Button("Back", _btn))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 4. Browse Lobbies
        // ─────────────────────────────────────────

        private void DrawBrowseLobbies()
        {
            GUILayout.Label("Browse Lobbies", _title);
            GUILayout.Space(8);

            GUI.enabled = !_loading;
            if (GUILayout.Button("Refresh", _btn, GUILayout.Width(120)))
            {
                RunAsync(async () =>
                {
                    _queryResults = await _provider.QueryLobbies();
                });
            }
            GUI.enabled = true;

            GUILayout.Space(4);

            if (_queryResults != null && _queryResults.Count > 0)
            {
                // Header
                GUILayout.BeginHorizontal(_rowEven);
                GUILayout.Label("Name", _headerLabel, GUILayout.Width(200));
                GUILayout.Label("Players", _headerLabel, GUILayout.Width(80));
                GUILayout.Label("Code", _headerLabel, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                for (int i = 0; i < _queryResults.Count; i++)
                {
                    var info = _queryResults[i];
                    var rowStyle = i % 2 == 0 ? _rowEven : _rowOdd;

                    GUILayout.BeginHorizontal(rowStyle);
                    GUILayout.Label(info.name ?? info.id ?? "", GUILayout.Width(200));
                    GUILayout.Label($"{info.playerCount}/{info.maxPlayers}", GUILayout.Width(80));
                    GUILayout.Label(info.code ?? "-", GUILayout.Width(100));
                    GUILayout.FlexibleSpace();

                    GUI.enabled = !_loading;
                    if (GUILayout.Button("Join", GUILayout.Width(50)))
                    {
                        var lobbyId = info.id;
                        RunAsync(async () =>
                        {
                            _lobby = await _provider.JoinLobby(lobbyId);
                            SubscribeLobbyEvents(_lobby);
                            _chatMessages.Clear();
                            _lastConnectionInfo = null;
                        }, MenuState.InLobby);
                    }
                    GUI.enabled = true;

                    GUILayout.EndHorizontal();
                }
            }
            else if (_queryResults != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("No lobbies found.", _subtitle);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Back", _btn))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 5. Join by Code
        // ─────────────────────────────────────────

        private void DrawJoinByCode()
        {
            GUILayout.Label("Join by Code", _title);
            GUILayout.Space(12);

            GUILayout.Label("Lobby Code", _subtitle);
            _joinCode = GUILayout.TextField(_joinCode, _field);

            GUILayout.Space(8);

            GUI.enabled = !_loading && !string.IsNullOrWhiteSpace(_joinCode);
            if (GUILayout.Button("Join", _btn))
            {
                var code = _joinCode;
                RunAsync(async () =>
                {
                    _lobby = await _provider.JoinLobbyByCode(code);
                    SubscribeLobbyEvents(_lobby);
                    _chatMessages.Clear();
                    _lastConnectionInfo = null;
                }, MenuState.InLobby);
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            if (GUILayout.Button("Back", _btn))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 6. In Lobby
        // ─────────────────────────────────────────

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

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    var rowStyle = i % 2 == 0 ? _rowEven : _rowOdd;

                    GUILayout.BeginHorizontal(rowStyle);
                    GUILayout.Label(p.displayName ?? "", GUILayout.Width(200));

                    if (p.isHost)
                    {
                        var c = GUI.color;
                        GUI.color = new Color(1f, 0.85f, 0.2f);
                        GUILayout.Label("Host", GUILayout.Width(60));
                        GUI.color = c;
                    }
                    else
                    {
                        GUILayout.Label("Player", _subtitle, GUILayout.Width(60));
                    }

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
            }

            GUILayout.Space(4);
            DrawLobbyChat();
            GUILayout.Space(4);
            DrawGameStarter();

            GUILayout.Space(8);
            DrawSeparator();
            GUILayout.Space(4);

            if (GUILayout.Button("Leave Lobby", _btn))
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
            _chatInput = GUILayout.TextField(_chatInput, _field);
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

        private void DrawGameStarter()
        {
            if (_gameStarter == null) return;

            bool isHost = _lobby.localPlayer is { isHost: true };
            if (!isHost && !_lastConnectionInfo.HasValue) return;

            if (isHost)
            {
                GUI.enabled = !_loading;
                if (GUILayout.Button("Start Game", _btn))
                {
                    var req = new GameStartRequest { lobbyId = _lobby.id };
                    RunAsync(async () =>
                    {
                        var info = await _gameStarter.StartGame(req);
                        _lastConnectionInfo = info;
                    });
                }
                GUI.enabled = true;
            }

            if (_lastConnectionInfo.HasValue)
            {
                var ci = _lastConnectionInfo.Value;
                GUILayout.Space(4);
                var c = GUI.color;
                GUI.color = new Color(0.6f, 1f, 0.6f);
                GUILayout.Label("Game Ready", _headerLabel);
                GUI.color = c;
                GUILayout.Label($"  Address: {ci.serverAddress}:{ci.serverPort}");
                if (!string.IsNullOrEmpty(ci.connectionToken))
                    GUILayout.Label($"  Token: {ci.connectionToken}");
            }
        }

        // ─────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────

        private static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorLine(rect, new Color(0.3f, 0.3f, 0.3f, 0.6f));
        }

        private static void EditorLine(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
