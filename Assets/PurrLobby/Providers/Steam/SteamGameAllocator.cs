#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
using System.Threading.Tasks;
using UnityEngine;
#if STEAMWORKS && !DISABLESTEAMWORKS
using PurrNet.Logging;
using PurrNet.Steam;
#endif

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Game allocator for Steam peer-to-peer sessions: the lobby owner hosts, and
    /// clients connect to the owner's SteamID over Steam relay sockets.
    /// </summary>
    [ProviderDependency("com.rlabrecque.steamworks.net", "Steamworks.NET")]
    [CreateAssetMenu(menuName = "PurrLobby/Steam/Game Allocator", fileName = "Steam Game Allocator", order = -203)]
    public class SteamGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        public override Task LoadGame(ILobby lobby)
        {
            return LoadGameScene(_gameScene);
        }

#if STEAMWORKS && !DISABLESTEAMWORKS
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
#else
        public override Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            return Task.FromResult(GameStartResponse.Failure(
                "Steamworks.NET is not installed or Steam is unsupported on this platform."));
        }

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            Debug.LogError(
                $"[{name}] Steamworks.NET is not installed or Steam is unsupported on this platform.");
            return false;
        }
#endif
    }
}
