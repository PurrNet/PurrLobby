#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Steam;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Game allocator for Steam peer-to-peer sessions: the lobby owner hosts, and
    /// clients connect to the owner's SteamID over Steam relay sockets.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Steam/Game Allocator", fileName = "Steam Game Allocator", order = -203)]
    public class SteamGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        public override Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            if (!SteamRuntime.isInitialized)
                return Task.FromResult(GameStartResponse.Failure("Steam is not initialized."));

            var hostId = lobby?.owner?.id;
            if (string.IsNullOrEmpty(hostId))
                return Task.FromResult(GameStartResponse.Failure("The lobby has no owner to host the game."));

            return Task.FromResult(GameStartResponse.Success(new ConnectionInfo
            {
                serverAddress = hostId,
                hostId = hostId,
            }));
        }

        public override Task LoadGame(ILobby lobby)
        {
            return LoadGameScene(_gameScene);
        }

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            if (string.IsNullOrEmpty(connection.serverAddress))
            {
                PurrLogger.LogError("Connection info has no host SteamID.");
                return false;
            }

            var transport = manager.transport as SteamTransport ?? GetOrAddComponent<SteamTransport>(manager.gameObject);
            manager.transport = transport;

            transport.peerToPeer = true;
            transport.dedicatedServer = false;
            transport.address = connection.serverAddress;
            return true;
        }
    }
}
#endif
