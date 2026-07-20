#if NAKAMA
using System;
using System.Threading.Tasks;
using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Game allocator using Nakama relayed matches for peer-hosted gameplay.</summary>
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Game Allocator", fileName = "Nakama Game Allocator", order = -202)]
    public class NakamaGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        [Tooltip("If true, the host listens for game readiness via the lobby's metadata before connecting. Disabled by default — most flows pre-load the scene and then connect immediately.")]
        [SerializeField] private bool _waitForGameStartFlag = false;

        public override async Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            var conn = NakamaConnection.instance;
            if (conn.socket == null || !conn.isSocketConnected)
                return GameStartResponse.Failure("Nakama socket is not connected. The session provider must finish login before allocating a game.");

            try
            {
                if (lobby is NakamaLobby nakamaLobby)
                {
                    return GameStartResponse.Success(new ConnectionInfo
                    {
                        serverAddress = nakamaLobby.id,
                        hostId = nakamaLobby.owner?.id,
                    });
                }

                var match = await conn.socket.CreateMatchAsync();
                return GameStartResponse.Success(new ConnectionInfo
                {
                    serverAddress = match.Id,
                });
            }
            catch (Exception e)
            {
                return GameStartResponse.Failure($"Failed to create gameplay match: {e.Message}");
            }
        }

        public override Task LoadGame(ILobby lobby)
        {
            if (_waitForGameStartFlag && lobby != null && lobby.isOwner)
                lobby.lobbyData.SetData(GameStartKeys.Status, "loading");

            return LoadGameScene(_gameScene);
        }

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            var conn = NakamaConnection.instance;
            if (conn.socket == null || !conn.isSocketConnected)
            {
                PurrLogger.LogError("Nakama socket is not connected. The session provider must finish login before starting a game.");
                return false;
            }

            if (string.IsNullOrEmpty(connection.serverAddress))
            {
                PurrLogger.LogError("Connection info has no Nakama match id. The host failed to allocate a gameplay match.");
                return false;
            }

            var nakamaTransport = manager.transport as PurrNet.Nakama.NakamaTransport
                                  ?? GetOrAddComponent<PurrNet.Nakama.NakamaTransport>(manager.gameObject);
            manager.transport = nakamaTransport;

            nakamaTransport.socket = conn.socket;
            nakamaTransport.matchId = connection.serverAddress;
            nakamaTransport.hostUserId = connection.hostId;
            return true;
        }
    }
}
#endif
