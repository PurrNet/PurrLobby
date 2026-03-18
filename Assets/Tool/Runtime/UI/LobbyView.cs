using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet.UI;
using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyView : MonoView
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private PlayerEntry _playerPrefab;
        [SerializeField] private GameObject _playerPlaceholderPrefab;
        [SerializeField] private RectTransform _playerContent;
        [SerializeField] private LobbyChat _chat;
        [SerializeField] private TMP_InputField _lobbyCode;
        [Space]
        [SerializeField] private Color _readyColor;
        [SerializeField] private Color _readyHover;
        [SerializeField] private Color _unreadyColor;
        [SerializeField] private Color _unreadyHover;
        [SerializeField] private TMP_Text _readyButtonText;
        [SerializeField] private ButtonElement _readyButton;
        [Space]
        [SerializeField] private Color _unmutedColor;
        [SerializeField] private Color _unmutedHover;
        [SerializeField] private Color _mutedColor;
        [SerializeField] private Color _mutedHover;
        [SerializeField] private ButtonElement _microphoneButton;
        [SerializeField] private TMP_Text _microphoneText;
        [SerializeField] private GameObject _microphoneFeature;
        [Space]
        [SerializeField] private bool _connectToPurrnetInLobby = true;
        [SerializeField] private LobbyConnectionProvider _lobbyConnection;

        private ILobby _lobby;
        private bool _lobbyConnected;
        private bool _hasLocalMicEnabled;

        private UIPool<Transform> _playerPlaceholderPool;

        public ILobby lobby => _lobby;

        public bool localMicEnabled => _hasLocalMicEnabled;

        public event Action<bool> onLocalMicEnabledChanged;

        public void Setup(ILobby lobby)
        {
            _playerPlaceholderPool ??= new UIPool<Transform>(_playerPlaceholderPrefab.transform, _playerContent);

            _lobby = lobby;

            RenderPlayerList(lobby);

            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
            _lobby.onPlayerUpdated += OnPlayerUpdated;
            _lobby.onLobbyDestroyed += OnLobbyDestroyed;
            _lobby.onHostChanged += OnHostChanged;

            _chat.Setup(lobby);
            _lobbyCode.text = lobby.joinCode;

            _microphoneFeature.SetActive(false);
            ConnectToLobby(lobby);
            UpdateMicGraphics();
        }

        public void EnableMicrophoneFeature()
        {
            _microphoneFeature.SetActive(true);
        }

        public void ToggleMicrophone()
        {
            _hasLocalMicEnabled = !_hasLocalMicEnabled;
            onLocalMicEnabledChanged?.Invoke(_hasLocalMicEnabled);
            UpdateMicGraphics();
        }

        private void UpdateMicGraphics()
        {
            _microphoneText.text = _hasLocalMicEnabled ? "<icon=microphone>" : "<icon=microphone_off>";

            var color = _hasLocalMicEnabled ? _unmutedColor : _mutedColor;
            var highlight = _hasLocalMicEnabled ? _unmutedHover : _mutedHover;

            _microphoneButton.backgroundNormal = color;
            _microphoneButton.backgroundHover = highlight;
        }

        private void ConnectToLobby(ILobby lobby)
        {
            if (_connectToPurrnetInLobby && _lobby.localPlayer != null && !_lobbyConnected && _lobbyConnection)
            {
                _lobbyConnection.JoinedLobby(lobby);
                _lobbyConnected = true;
            }
        }

        private void OnPlayerUpdated(IPlayer player)
        {
            UpdateLocalPlayerData(_lobby);
        }

        public void CopyLobbyCodeToClipboard()
        {
            GUIUtility.systemCopyBuffer = _lobby.joinCode;
            Toaster.Push("Lobby Code", "Code copied to clipboard!");
        }

        private bool _wasReady = true;

        private void RenderPlayerList(ILobby lobby)
        {
            _playerPlaceholderPool.ResetCounter();

            for (int i = 0; i < lobby.maxPlayers; i++)
            {
                var player = i < lobby.players.Count ? lobby.players[i] : null;

                if (player == null)
                    _playerPlaceholderPool.GetInstance().SetAsLastSibling();
            }

            _playerPlaceholderPool.DiscardRest();

            UpdateLocalPlayerData(lobby);
        }

        private void UpdateLocalPlayerData(ILobby lobby)
        {
            bool localPlayerReady = lobby.localPlayer?.isReady == true;

            if (_wasReady != localPlayerReady)
            {
                _readyButtonText.text = localPlayerReady ? "Unready" : "Ready";
                _readyButton.backgroundNormal = localPlayerReady ? _readyColor : _unreadyColor;
                _readyButton.backgroundHover = localPlayerReady ? _readyHover : _unreadyHover;
                _wasReady = localPlayerReady;
            }
        }

        public void ToggleReady()
        {
            if (_lobby?.localPlayer == null)
            {
                Toaster.PushError("Lobby Error", "Your player isn't connected yet.");
                return;
            }

            _lobby.localPlayer.SetReady(!_lobby.localPlayer.isReady);
        }

        private void OnKickPlayer(IPlayer target)
        {
            _lobby.KickPlayer(target);
        }

        public override void OnPopped()
        {
            if (_lobby != null)
            {
                _lobby.onPlayerJoined -= OnPlayerJoined;
                _lobby.onPlayerLeft -= OnPlayerLeft;
                _lobby.onPlayerUpdated -= OnPlayerUpdated;
                _lobby.onLobbyDestroyed -= OnLobbyDestroyed;
                _lobby.onHostChanged -= OnHostChanged;

                if (_lobbyConnected && _lobbyConnection)
                {
                    _lobbyConnection.LeftLobby(_lobby);
                    _lobbyConnected = false;
                }
            }
        }

        private void OnHostChanged(IPlayer host)
        {
            if (_lobbyConnected && _lobbyConnection)
                _lobbyConnection.OnHostChanged(_lobby, host, host == _lobby.localPlayer);
        }

        private void OnLobbyDestroyed()
        {
            PopMe();
        }

        public void LeaveLobby()
        {
            _lobby.LeaveLobby();
            PopMe();
        }

        private readonly Dictionary<IPlayer, PlayerEntry> _uiPlayerEntry = new();

        private void OnPlayerJoined(IPlayer player)
        {
            var entry = Instantiate(_playerPrefab, _playerContent);
            entry.Setup(_lobby, player, OnKickPlayer);
            _uiPlayerEntry.Add(player, entry);

            RenderPlayerList(_lobby);
            ConnectToLobby(_lobby);

            if (_lobbyConnected)
                _lobbyConnection.OnPlayerRegistered(player);
        }

        private void OnPlayerLeft(IPlayer player)
        {
            if (_uiPlayerEntry.Remove(player, out var entry))
                Destroy(entry.gameObject);

            RenderPlayerList(_lobby);

            if (_lobbyConnected)
                _lobbyConnection.OnPlayerUnregistered(player);
        }

        protected override IEnumerator OnExitTransition()
        {
            return ViewTransitions.SlideToRight(_content);
        }

        protected override IEnumerator OnEnterTransition()
        {
            return ViewTransitions.SlideFromRight(_content);
        }

        public bool TryGetPlayerEntry(IPlayer player, out PlayerEntry entry)
        {
            return _uiPlayerEntry.TryGetValue(player, out entry);
        }
    }
}
