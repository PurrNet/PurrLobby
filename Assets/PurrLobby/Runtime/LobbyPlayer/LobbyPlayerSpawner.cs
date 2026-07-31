using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkManager _manager;
        [SerializeField] private PurrLobbyPlayer _playerViewPrefab;

        readonly Dictionary<PlayerID, PurrLobbyPlayer> _players = new ();

        private void OnEnable()
        {
            _manager.onPlayerLoadedScene += OnPlayerJoined;
            _manager.onPlayerUnloadedScene += OnPlayerLeft;
        }

        private void OnDisable()
        {
            _manager.onPlayerLoadedScene -= OnPlayerJoined;
            _manager.onPlayerUnloadedScene -= OnPlayerLeft;
        }

        private void OnPlayerLeft(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer) return;

            if (!_manager.sceneModule.TryGetSceneID(gameObject.scene, out var sceneID) || sceneID != scene)
                return;

            // The identity may already be gone (network despawn or scene teardown
            // destroys it before this fires) — only destroy what's still alive.
            if (_players.Remove(player, out var lobbyPlayer) && lobbyPlayer)
                Destroy(lobbyPlayer.gameObject);
        }

        private void OnPlayerJoined(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer)
                return;

            if (!_manager.sceneModule.TryGetSceneID(gameObject.scene, out var sceneID) || sceneID != scene)
                return;

            if (!_manager.playerModule.TryGetConnection(player, out var connection))
            {
                PurrLogger.LogError($"Player {player} is not connected");
                return;
            }

            if (_manager.authenticator is IProvideConnectionToPlayerID provider)
            {
                var lobbyPlayer = Instantiate(_playerViewPrefab);
                lobbyPlayer.GiveOwnership(player);
                _players[player] = lobbyPlayer;
                if (provider.TryGetPlayerID(connection, out var id))
                    lobbyPlayer.Setup(id);
            }
            else
            {
                PurrLogger.LogError($"`NetworkAuthenticator` does not implement `{nameof(IProvideConnectionToPlayerID)}`, cannot spawn lobby player.");
            }
        }
    }
}
