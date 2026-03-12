using System.Collections;
using PurrNet.UI;
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

        private ILobby _lobby;

        private UIPool<PlayerEntry> _playerEntryPool;
        private UIPool<Transform> _playerPlaceholderPool;

        public void Setup(ILobby lobby)
        {
            _playerEntryPool ??= new UIPool<PlayerEntry>(_playerPrefab, _playerContent);
            _playerPlaceholderPool ??= new UIPool<Transform>(_playerPlaceholderPrefab.transform, _playerContent);

            _lobby = lobby;

            RenderPlayerList(lobby);

            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
            _lobby.onLobbyDestroyed += OnLobbyDestroyed;

            _chat.Setup(lobby);
        }

        private void RenderPlayerList(ILobby lobby)
        {
            _playerEntryPool.ResetCounter();
            _playerPlaceholderPool.ResetCounter();

            for (int i = 0; i < lobby.maxPlayers; i++)
            {
                var player = i < lobby.players.Count ? lobby.players[i] : null;

                if (player == null)
                {
                    _playerPlaceholderPool.GetInstance().SetAsLastSibling();
                }
                else
                {
                    var entry = _playerEntryPool.GetInstance();
                    entry.transform.SetAsLastSibling();
                    entry.Setup(lobby.localPlayer, player, OnKickPlayer);
                }
            }

            _playerEntryPool.DiscardRest();
            _playerPlaceholderPool.DiscardRest();
        }

        private void OnKickPlayer(IPlayer target)
        {
            _lobby.KickPlayer(target);
        }

        public override void OnPopped()
        {
            _lobby.onPlayerJoined -= OnPlayerJoined;
            _lobby.onPlayerLeft -= OnPlayerLeft;
            _lobby.onLobbyDestroyed -= OnLobbyDestroyed;
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

        private void OnPlayerJoined(IPlayer player)
        {
            RenderPlayerList(_lobby);
        }

        private void OnPlayerLeft(IPlayer player)
        {
            RenderPlayerList(_lobby);
        }

        protected override IEnumerator OnExitTransition()
        {
            return ViewTransitions.SlideToRight(_content);
        }

        protected override IEnumerator OnEnterTransition()
        {
            return ViewTransitions.SlideFromRight(_content);
        }
    }
}
