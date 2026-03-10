using PurrLobby;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyView : MonoView
    {
        private LobbyProvider _lobbyProvider;
        private ILobby _lobby;

        public void Setup(MenuOrchestrator orchestrator, ILobby lobby)
        {
            _lobbyProvider = orchestrator.lobbyProvider;
            _lobby = lobby;

            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
        }

        public override void OnPopped()
        {
            _lobby.onPlayerJoined -= OnPlayerJoined;
            _lobby.onPlayerLeft -= OnPlayerLeft;
        }

        private void OnPlayerJoined(IPlayer player)
        {
            Debug.Log($"Player joined: {player.displayName}");
        }

        private void OnPlayerLeft(IPlayer player)
        {
            Debug.Log($"Player left: {player.displayName}");
        }
    }
}
