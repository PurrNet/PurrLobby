using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using ImGuiNET.Unity;
using UnityEngine;

namespace PurrLobby
{
    public sealed class PurrLobbyDebugPanel : MonoBehaviour
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
        private string _playerId = System.Guid.NewGuid().ToString("N")[..8];
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

        // Game starter result
        private ConnectionInfo? _lastConnectionInfo;

        // Layout constants
        const float BUTTON_HEIGHT = 36f;
        const float SPACING = 8f;

        private void OnEnable()
        {
            DearImGui.AddDrawRequest(OnShouldRender);
            ImGuiUn.Layout += OnImGUI;
        }

        private void OnDisable()
        {
            DearImGui.RemoveDrawRequest(OnShouldRender);
            ImGuiUn.Layout -= OnImGUI;
        }

        private bool OnShouldRender()
        {
            return enabled && gameObject.activeInHierarchy;
        }

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
                    var text = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
                    _chatMessages.Add($"[{DateTime.Now:HH:mm:ss}] {player.displayName}: {text}");
                };
            }
        }

        // ─────────────────────────────────────────
        // Main render entry
        // ─────────────────────────────────────────

        private void OnImGUI()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.Pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(viewport.Size, ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 5));

            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                      | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
                      | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground;

            if (!ImGui.Begin("PurrLobby##Fullscreen", flags))
            {
                ImGui.End();
                ImGui.PopStyleVar(2);
                return;
            }

            DrawStatusBar();

            GetCardSize(out float cardW, out float cardH);
            BeginCenteredCard(cardW, cardH);

            switch (_state)
            {
                case MenuState.Login:        DrawLogin(); break;
                case MenuState.MainMenu:     DrawMainMenu(); break;
                case MenuState.CreateLobby:  DrawCreateLobby(); break;
                case MenuState.BrowseLobbies:DrawBrowseLobbies(); break;
                case MenuState.JoinByCode:   DrawJoinByCode(); break;
                case MenuState.InLobby:      DrawInLobby(); break;
            }

            EndCenteredCard();

            DrawErrorToast();

            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        // ─────────────────────────────────────────
        // Status bar (always visible)
        // ─────────────────────────────────────────

        private void DrawStatusBar()
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

            // Measure width for the background pill
            float contentW = ImGui.CalcTextSize(stateLabel).x;
            if (_provider != null)
                contentW += ImGui.CalcTextSize(_playerName).x + ImGui.CalcTextSize(" | ").x;
            if (_loading)
                contentW += ImGui.CalcTextSize(" | Working...").x;

            float pillH = ImGui.GetTextLineHeightWithSpacing() + 10;
            float pillW = contentW + 24;

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.12f, 0.85f));
            ImGui.BeginChild("StatusBar", new Vector2(pillW, pillH), true);

            // Player name + state
            if (_provider != null)
            {
                ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), _playerName);
                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();
            }

            ImGui.TextDisabled(stateLabel);

            // Loading spinner
            if (_loading)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();
                float t = (float)ImGui.GetTime();
                int dots = ((int)(t * 3f)) % 4;
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Working" + new string('.', dots));
            }

            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        // ─────────────────────────────────────────
        // Error toast (bottom-right, fades out)
        // ─────────────────────────────────────────

        private void DrawErrorToast()
        {
            if (string.IsNullOrEmpty(_errorMessage)) return;

            const float TOAST_WIDTH = 350f;
            const float TOAST_PAD = 16f;

            float alpha = Mathf.Clamp01(_errorTimer / 0.5f);
            var windowSize = ImGui.GetWindowSize();
            float toastH = ImGui.GetTextLineHeightWithSpacing() + 16;

            float x = windowSize.x - TOAST_WIDTH - TOAST_PAD;
            float y = windowSize.y - toastH - TOAST_PAD;
            ImGui.SetCursorPos(new Vector2(x, y));

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.6f, 0.1f, 0.1f, 0.9f * alpha));
            if (ImGui.BeginChild("ErrorToast", new Vector2(TOAST_WIDTH, toastH), true))
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, alpha), _errorMessage);
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        // ─────────────────────────────────────────
        // 1. Login Screen
        // ─────────────────────────────────────────

        private void DrawLogin()
        {
            CenterText("PurrLobby", 1.5f);
            ImGui.Dummy(new Vector2(0, SPACING * 2));

            float w = ImGui.GetContentRegionAvail().x;

            ImGui.SetNextItemWidth(w);
            ImGui.InputText("##PlayerName", ref _playerName, 128);
            ImGui.TextDisabled("Player Name");

            ImGui.Dummy(new Vector2(0, 4));

            ImGui.SetNextItemWidth(w);
            ImGui.InputText("##PlayerId", ref _playerId, 128);
            ImGui.TextDisabled("Player ID");

            ImGui.Dummy(new Vector2(0, SPACING));

            if (settings != null)
            {
                ImGui.TextDisabled($"API: {settings.apiUrl}");
                ImGui.TextDisabled($"Game: {settings.gameId}");
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No PurrLobbySettings assigned!");
            }

            ImGui.Dummy(new Vector2(0, SPACING));

            bool disabled = _loading || settings == null;
            if (disabled) ImGui.BeginDisabled();

            if (ImGui.Button("Connect", new Vector2(w, BUTTON_HEIGHT)))
            {
                _provider = settings.CreateProvider(_playerId, _playerName);
                _gameStarter = settings.CreateGameStarter(_playerId, _playerName);
                _state = MenuState.MainMenu;
            }

            if (disabled) ImGui.EndDisabled();
        }

        // ─────────────────────────────────────────
        // 2. Main Menu
        // ─────────────────────────────────────────

        private void DrawMainMenu()
        {
            CenterText($"Welcome, {_playerName}!", 1.2f);
            ImGui.Dummy(new Vector2(0, SPACING * 2));

            float w = ImGui.GetContentRegionAvail().x;

            if (_loading) ImGui.BeginDisabled();

            if (ImGui.Button("Create Lobby", new Vector2(w, BUTTON_HEIGHT)))
                _state = MenuState.CreateLobby;

            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Browse Lobbies", new Vector2(w, BUTTON_HEIGHT)))
            {
                RunAsync(async () =>
                {
                    _queryResults = await _provider.QueryLobbies();
                }, MenuState.BrowseLobbies);
            }

            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Join by Code", new Vector2(w, BUTTON_HEIGHT)))
                _state = MenuState.JoinByCode;

            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Quick Play", new Vector2(w, BUTTON_HEIGHT)))
            {
                RunAsync(async () =>
                {
                    _lobby = await _provider.JoinRandom();
                    SubscribeLobbyEvents(_lobby);
                }, MenuState.InLobby);
            }

            if (_loading) ImGui.EndDisabled();

            ImGui.Dummy(new Vector2(0, SPACING * 3));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Disconnect", new Vector2(w, BUTTON_HEIGHT)))
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
        // 3. Create Lobby Screen
        // ─────────────────────────────────────────

        private void DrawCreateLobby()
        {
            CenterText("Create Lobby", 1.2f);
            ImGui.Dummy(new Vector2(0, SPACING));

            float w = ImGui.GetContentRegionAvail().x;

            ImGui.SetNextItemWidth(w);
            ImGui.InputText("##LobbyName", ref _lobbyName, 128);
            ImGui.TextDisabled("Lobby Name");

            ImGui.Dummy(new Vector2(0, 4));

            ImGui.SetNextItemWidth(w);
            ImGui.SliderInt("##MaxPlayers", ref _maxPlayers, 2, 16, "%d players");
            ImGui.TextDisabled("Max Players");

            ImGui.Dummy(new Vector2(0, 4));

            var visNames = new[] { "Public", "Private" };
            ImGui.SetNextItemWidth(w);
            ImGui.Combo("##Visibility", ref _visibilityIndex, visNames, visNames.Length);
            ImGui.TextDisabled("Visibility");

            ImGui.Dummy(new Vector2(0, SPACING));

            if (_loading) ImGui.BeginDisabled();

            if (ImGui.Button("Create", new Vector2(w, BUTTON_HEIGHT)))
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

            if (_loading) ImGui.EndDisabled();

            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Back", new Vector2(w, BUTTON_HEIGHT)))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 4. Browse Lobbies Screen
        // ─────────────────────────────────────────

        private void DrawBrowseLobbies()
        {
            CenterText("Browse Lobbies", 1.2f);
            ImGui.Dummy(new Vector2(0, SPACING));

            float w = ImGui.GetContentRegionAvail().x;

            if (_loading) ImGui.BeginDisabled();

            if (ImGui.Button("Refresh", new Vector2(120, BUTTON_HEIGHT)))
            {
                RunAsync(async () =>
                {
                    _queryResults = await _provider.QueryLobbies();
                });
            }

            if (_loading) ImGui.EndDisabled();

            ImGui.Dummy(new Vector2(0, 4));

            if (_queryResults != null && _queryResults.Count > 0)
            {
                if (ImGui.BeginTable("Lobbies", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 3f);
                    ImGui.TableSetupColumn("Players", ImGuiTableColumnFlags.None, 1.5f);
                    ImGui.TableSetupColumn("Code", ImGuiTableColumnFlags.None, 1.5f);
                    ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.None, 1f);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < _queryResults.Count; i++)
                    {
                        var info = _queryResults[i];
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(info.name ?? info.id ?? "");

                        ImGui.TableNextColumn();
                        ImGui.Text($"{info.playerCount}/{info.maxPlayers}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(info.code ?? "-");

                        ImGui.TableNextColumn();
                        if (_loading) ImGui.BeginDisabled();
                        if (ImGui.SmallButton($"Join##{i}"))
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
                        if (_loading) ImGui.EndDisabled();
                    }

                    ImGui.EndTable();
                }
            }
            else if (_queryResults != null)
            {
                ImGui.Dummy(new Vector2(0, SPACING));
                ImGui.TextDisabled("No lobbies found.");
            }

            ImGui.Dummy(new Vector2(0, SPACING));

            if (ImGui.Button("Back", new Vector2(w, BUTTON_HEIGHT)))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 5. Join by Code Screen
        // ─────────────────────────────────────────

        private void DrawJoinByCode()
        {
            CenterText("Join by Code", 1.2f);
            ImGui.Dummy(new Vector2(0, SPACING * 2));

            float w = ImGui.GetContentRegionAvail().x;

            ImGui.SetNextItemWidth(w);
            ImGui.InputText("##Code", ref _joinCode, 64);
            ImGui.TextDisabled("Lobby Code");

            ImGui.Dummy(new Vector2(0, SPACING));

            bool canJoin = !_loading && !string.IsNullOrWhiteSpace(_joinCode);
            if (!canJoin) ImGui.BeginDisabled();

            if (ImGui.Button("Join", new Vector2(w, BUTTON_HEIGHT)))
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

            if (!canJoin) ImGui.EndDisabled();

            ImGui.Dummy(new Vector2(0, 4));

            if (ImGui.Button("Back", new Vector2(w, BUTTON_HEIGHT)))
                _state = MenuState.MainMenu;
        }

        // ─────────────────────────────────────────
        // 6. In Lobby / Waiting Room
        // ─────────────────────────────────────────

        private void DrawInLobby()
        {
            if (_lobby == null)
            {
                _state = MenuState.MainMenu;
                return;
            }

            float w = ImGui.GetContentRegionAvail().x;

            // Header
            ImGui.Text($"Lobby: {_lobby.id}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy ID"))
                GUIUtility.systemCopyBuffer = _lobby.id;

            ImGui.Text($"Max Players: {_lobby.maxPlayers}");

            ImGui.Dummy(new Vector2(0, 4));

            // Player list
            ImGui.Text("Players");
            ImGui.Separator();

            var players = _lobby.players;
            if (players != null && ImGui.BeginTable("Players", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 3f);
                ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.None, 1.5f);
                ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.None, 1f);
                ImGui.TableHeadersRow();

                bool isLocalHost = _lobby.localPlayer != null && _lobby.localPlayer.isHost;

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(p.displayName ?? "");

                    ImGui.TableNextColumn();
                    if (p.isHost)
                        ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "Host");
                    else
                        ImGui.TextDisabled("Player");

                    ImGui.TableNextColumn();
                    bool isSelf = _lobby.localPlayer != null && p.id == _lobby.localPlayer.id;
                    if (isLocalHost && !isSelf)
                    {
                        if (ImGui.SmallButton($"Kick##{i}"))
                        {
                            try { _lobby.KickPlayer(p); }
                            catch (Exception ex) { ShowError(ex.Message); }
                        }
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Dummy(new Vector2(0, 4));

            // Chat
            DrawLobbyChat(w);

            ImGui.Dummy(new Vector2(0, 4));

            // Game Starter (host only)
            DrawGameStarter(w);

            ImGui.Dummy(new Vector2(0, 4));

            // Advanced section (metadata)
            if (ImGui.CollapsingHeader("Advanced"))
                DrawMetadataEditors();

            ImGui.Dummy(new Vector2(0, SPACING));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 4));

            // Leave button
            if (ImGui.Button("Leave Lobby", new Vector2(w, BUTTON_HEIGHT)))
            {
                try
                {
                    _lobby.LeaveLobby();
                    _lobby = null;
                    _chatMessages.Clear();
                    _lastConnectionInfo = null;
                    _state = MenuState.MainMenu;
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
        }

        private void DrawLobbyChat(float w)
        {
            if (_lobby?.chat == null) return;

            ImGui.Text("Chat");
            ImGui.Separator();

            float chatHeight = ImGui.GetTextLineHeightWithSpacing() * 6;
            if (ImGui.BeginChild("ChatLog", new Vector2(0, chatHeight), true))
            {
                for (int i = 0; i < _chatMessages.Count; i++)
                    ImGui.TextUnformatted(_chatMessages[i]);

                if (_chatMessages.Count > 0)
                    ImGui.SetScrollHereY(1.0f);
            }
            ImGui.EndChild();

            ImGui.SetNextItemWidth(w - 60);
            bool enter = ImGui.InputText("##ChatInput", ref _chatInput, 512, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();

            bool send = ImGui.Button("Send", new Vector2(52, 0)) || enter;
            if (send && !string.IsNullOrEmpty(_chatInput))
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

        private void DrawGameStarter(float w)
        {
            if (_gameStarter == null) return;

            bool isHost = _lobby.localPlayer != null && _lobby.localPlayer.isHost;
            if (!isHost && !_lastConnectionInfo.HasValue) return;

            if (isHost)
            {
                if (_loading) ImGui.BeginDisabled();

                if (ImGui.Button("Start Game", new Vector2(w, BUTTON_HEIGHT)))
                {
                    var req = new GameStartRequest { lobbyId = _lobby.id };
                    RunAsync(async () =>
                    {
                        var info = await _gameStarter.StartGame(req);
                        _lastConnectionInfo = info;
                    });
                }

                if (_loading) ImGui.EndDisabled();
            }

            if (_lastConnectionInfo.HasValue)
            {
                var ci = _lastConnectionInfo.Value;
                ImGui.Dummy(new Vector2(0, 4));
                ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), "Game Ready");
                ImGui.Text($"  Address: {ci.serverAddress}:{ci.serverPort}");
                if (!string.IsNullOrEmpty(ci.connectionToken))
                    ImGui.Text($"  Token: {ci.connectionToken}");
            }
        }

        private void DrawMetadataEditors()
        {
            if (_lobby == null) return;

            // Lobby metadata
            if (_lobby.lobbyData != null)
            {
                ImGui.Text("Lobby Metadata");
                ImGui.InputText("Key##lobby", ref _lobbyMetaKey, 128);
                ImGui.SameLine();
                ImGui.InputText("Value##lobby", ref _lobbyMetaValue, 256);
                ImGui.SameLine();
                if (ImGui.SmallButton("Set##lobby"))
                {
                    try { _lobby.lobbyData.SetData(_lobbyMetaKey, _lobbyMetaValue); }
                    catch (Exception ex) { ShowError(ex.Message); }
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove##lobby"))
                {
                    try { _lobby.lobbyData.RemoveData(_lobbyMetaKey); }
                    catch (Exception ex) { ShowError(ex.Message); }
                }

                ImGui.Separator();
            }

            // Player metadata
            if (_lobby.localPlayer?.userData != null)
            {
                ImGui.Text("Player Metadata");
                ImGui.InputText("Key##player", ref _playerMetaKey, 128);
                ImGui.SameLine();
                ImGui.InputText("Value##player", ref _playerMetaValue, 256);
                ImGui.SameLine();
                if (ImGui.SmallButton("Set##player"))
                {
                    try { _lobby.localPlayer.userData.SetData(_playerMetaKey, _playerMetaValue); }
                    catch (Exception ex) { ShowError(ex.Message); }
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove##player"))
                {
                    try { _lobby.localPlayer.userData.RemoveData(_playerMetaKey); }
                    catch (Exception ex) { ShowError(ex.Message); }
                }
            }
        }

        // ─────────────────────────────────────────
        // Fullscreen card helpers
        // ─────────────────────────────────────────

        private void GetCardSize(out float width, out float height)
        {
            switch (_state)
            {
                case MenuState.Login:         width = 400; height = 280; break;
                case MenuState.MainMenu:      width = 400; height = 360; break;
                case MenuState.CreateLobby:   width = 400; height = 300; break;
                case MenuState.BrowseLobbies: width = 550; height = 420; break;
                case MenuState.JoinByCode:    width = 400; height = 300; break;
                case MenuState.InLobby:       width = 550; height = 500; break;
                default:                      width = 400; height = 300; break;
            }
        }

        private static void BeginCenteredCard(float width, float height)
        {
            var windowSize = ImGui.GetWindowSize();
            float x = (windowSize.x - width) * 0.5f;
            float y = (windowSize.y - height) * 0.5f;
            ImGui.SetCursorPos(new Vector2(x, y));

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.12f, 0.15f, 0.95f));
            ImGui.BeginChild("Card", new Vector2(width, height), true);
        }

        private static void EndCenteredCard()
        {
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        // ─────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────

        private static void CenterText(string text, float scale = 1f)
        {
            if (scale > 1f)
                ImGui.SetWindowFontScale(scale);

            float textWidth = ImGui.CalcTextSize(text).x;
            float regionWidth = ImGui.GetContentRegionAvail().x;
            float offset = (regionWidth - textWidth) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

            ImGui.Text(text);

            if (scale > 1f)
                ImGui.SetWindowFontScale(1f);
        }
    }
}
