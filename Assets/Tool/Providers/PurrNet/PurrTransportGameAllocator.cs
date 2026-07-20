using System.Threading.Tasks;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    [CreateAssetMenu(menuName = "PurrLobby/PurrNet/Game Allocator", fileName = "PurrTransport Game Allocator", order = -200)]
    public class PurrTransportGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

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

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            var transport = manager.transport as PurrTransport ?? GetOrAddComponent<PurrTransport>(manager.gameObject);
            manager.transport = transport;
            transport.roomName = connection.serverAddress;
            return true;
        }
    }
}
