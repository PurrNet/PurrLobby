using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Transports;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.GenericProviders
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Game Allocator", fileName = "PurrTransport Game Allocator", order = -200)]
    public class PurrTransportGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        public override Task Login(ViewStack stack) => Task.CompletedTask;

        public override void Logout() { }

        public override Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            return Task.FromResult(GameStartResponse.Success(new ConnectionInfo
            {
                serverAddress = lobby.id
            }));
        }

        public override Task LoadGame(ILobby lobby)
        {
            return LoadGameScene(_gameScene);
        }

        private T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent(out T component))
                return component;
            return gameObject.AddComponent<T>();
        }

        public override void Connect(ConnectionInfo connection, bool shouldBeHost)
        {
            var manager = NetworkManager.main;

            if (!manager)
            {
                PurrLogger.LogError("No `NetworkManager` found in the scene.");
                return;
            }

            if (manager.shouldAutoStartServer || manager.shouldAutoStartClient)
            {
                PurrLogger.LogError("`NetworkManager` is set to auto start (has auto start flags). Please disable auto start and try again.");
                return;
            }

            if (manager.transport is PurrTransport transport)
            {
                transport.roomName = connection.serverAddress;
            }
            else
            {
                var purrTransport = GetOrAddComponent<PurrTransport>(manager.gameObject);
                manager.transport = purrTransport;
                purrTransport.roomName = connection.serverAddress;
            }

            if (shouldBeHost)
                 manager.StartHost();
            else manager.StartClient();
        }
    }
}
