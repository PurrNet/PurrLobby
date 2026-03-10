using System.Collections;
using PurrLobby;
using PurrNet.UI;
using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyView : MonoView
    {
        [SerializeField] private RectTransform _content;

        private LobbyProvider _lobbyProvider;
        private ILobby _lobby;

        public void Setup(MenuOrchestrator orchestrator, ILobby lobby)
        {
            _lobbyProvider = orchestrator.lobbyProvider;
            _lobby = lobby;

            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
            _lobby.onLobbyDestroyed += OnLobbyDestroyed;
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
            Debug.Log($"Player joined: {player.displayName}");
        }

        private void OnPlayerLeft(IPlayer player)
        {
            Debug.Log($"Player left: {player.displayName}");
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
