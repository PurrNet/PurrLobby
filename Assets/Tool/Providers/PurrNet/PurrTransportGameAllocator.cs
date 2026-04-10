using System.Threading.Tasks;
using PurrNet.Transports;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby.GenericProviders
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Purr Transport Game Allocator", order = -200)]
    public class PurrTransportGameAllocator : GameAllocatorProvider
    {
        public override Task Login(ViewStack stack) => Task.CompletedTask;

        public override void Logout() { }

        public override Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            return Task.FromResult(GameStartResponse.Success(new ConnectionInfo
            {
                serverAddress = lobby.id
            }));
        }

        private T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent(out T component))
                return component;
            return gameObject.AddComponent<T>();
        }

        public override void Connect(NetworkManager manager, ConnectionInfo connection, bool shouldBeHost)
        {
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
