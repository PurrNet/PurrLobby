#if NAKAMA
using System;
using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>Game allocator using Nakama relayed matches for peer-hosted gameplay.</summary>
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Game Allocator", fileName = "Nakama Game Allocator", order = -202)]
    public class NakamaGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        [Tooltip("If true, the host listens for game readiness via the lobby's metadata before connecting. Disabled by default — most flows pre-load the scene and then connect immediately.")]
        [SerializeField] private bool _waitForGameStartFlag = false;

        public override Task Login(ViewStack stack) => Task.CompletedTask;

        public override void Logout() { }

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
            if (string.IsNullOrEmpty(_gameScene))
                throw new Exception($"Game scene is not set. Please set the game scene in the inspector of `{name}`.");

            if (_waitForGameStartFlag && lobby != null && lobby.isOwner)
                lobby.lobbyData.SetData(GameStartKeys.Status, "loading");

            var asyncOp = SceneManager.LoadSceneAsync(_gameScene);
            if (asyncOp == null)
            {
                PurrLogger.LogError($"Loading scene `{_gameScene}` failed.");
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            asyncOp.completed += _ => tcs.SetResult(true);
            return tcs.Task;
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

            var conn = NakamaConnection.instance;
            if (conn.socket == null || !conn.isSocketConnected)
            {
                PurrLogger.LogError("Nakama socket is not connected. The session provider must finish login before starting a game.");
                return;
            }

            if (string.IsNullOrEmpty(connection.serverAddress))
            {
                PurrLogger.LogError("Connection info has no Nakama match id. The host failed to allocate a gameplay match.");
                return;
            }

            PurrNet.Nakama.NakamaTransport nakamaTransport;
            if (manager.transport is PurrNet.Nakama.NakamaTransport existing)
            {
                nakamaTransport = existing;
            }
            else
            {
                nakamaTransport = GetOrAddComponent<PurrNet.Nakama.NakamaTransport>(manager.gameObject);
                manager.transport = nakamaTransport;
            }

            nakamaTransport.socket = conn.socket;
            nakamaTransport.matchId = connection.serverAddress;
            nakamaTransport.hostUserId = connection.hostId;

            if (shouldBeHost)
                manager.StartHost();
            else
                manager.StartClient();
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent(out T component))
                return component;
            return gameObject.AddComponent<T>();
        }
    }
}
#endif
