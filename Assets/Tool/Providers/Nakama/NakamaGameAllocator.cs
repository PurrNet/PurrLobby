#if NAKAMA
using System;
using System.Reflection;
using System.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Transports;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Hands off from the lobby to an in-game session.
    ///
    /// Nakama itself does not allocate dedicated servers — peer-hosting via a relay (PurrTransport,
    /// SteamRelay, the new <see cref="PurrNet.Nakama.NakamaTransport"/>, etc.) is the typical path.
    ///
    /// When the wired transport is <see cref="PurrNet.Nakama.NakamaTransport"/>, the allocator pre-
    /// creates a fresh Nakama match in <see cref="AllocateGame"/> using the shared session socket and
    /// passes its id through <see cref="ConnectionInfo.serverAddress"/>. Both host and client then
    /// adopt that match via the transport's matchId field. Other transports fall back to the generic
    /// reflection-based "room name" assignment using the lobby id.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Game Allocator", fileName = "Nakama Game Allocator", order = -202)]
    public class NakamaGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;

        [Tooltip("Field or property name on the active transport that receives the lobby id (used as the room/match identifier). Only used when the transport is not a NakamaTransport — leave empty to skip.")]
        [SerializeField] private string _transportRoomField = "roomName";

        [Tooltip("If true, the host listens for game readiness via the lobby's metadata before connecting. Disabled by default — most flows pre-load the scene and then connect immediately.")]
        [SerializeField] private bool _waitForGameStartFlag = false;

        [Tooltip("If true, when the Nakama session socket is up the host pre-creates a dedicated gameplay match in AllocateGame and passes its id through ConnectionInfo. Disable when pairing the Nakama session with a non-Nakama transport (e.g. PurrTransport, SteamRelay) to avoid leaving an unused match on the Nakama server.")]
        [SerializeField] private bool _preallocateNakamaMatch = true;

        public override Task Login(ViewStack stack) => Task.CompletedTask;

        public override void Logout() { }

        public override async Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            // For NakamaTransport we pre-create a dedicated gameplay match so its id is known up-front
            // and can be carried through the existing ConnectionInfo metadata flow. The active transport
            // is on the gameplay scene which isn't loaded yet — sniff intent by checking whether the
            // session/socket is up. If a NakamaTransport ends up wired in, Connect will adopt by id.
            var conn = NakamaConnection.instance;
            if (_preallocateNakamaMatch && conn.socket != null && conn.isSocketConnected)
            {
                try
                {
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

            // Fall back to the generic peer-hosted relay model: any transport keying off the lobby id.
            return GameStartResponse.Success(new ConnectionInfo
            {
                serverAddress = lobby.id,
            });
        }

        public override Task LoadGame(ILobby lobby)
        {
            if (string.IsNullOrEmpty(_gameScene))
                throw new Exception($"Game scene is not set. Please set the game scene in the inspector of `{name}`.");

            if (_waitForGameStartFlag && lobby.isHost)
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

            var transport = manager.transport;
            if (transport == null)
            {
                PurrLogger.LogError($"NetworkManager has no transport configured. Attach a transport (PurrTransport, NakamaTransport, etc.) before using `{name}`.");
                return;
            }

            if (transport is PurrNet.Nakama.NakamaTransport nakamaTransport)
            {
                var conn = NakamaConnection.instance;
                if (conn.socket == null || !conn.isSocketConnected)
                {
                    PurrLogger.LogError("[NakamaGameAllocator] Nakama socket is not connected. The session provider must finish login before starting a game.");
                    return;
                }

                if (string.IsNullOrEmpty(connection.serverAddress))
                {
                    PurrLogger.LogError("[NakamaGameAllocator] Connection info has no Nakama match id. The host failed to allocate a gameplay match.");
                    return;
                }

                nakamaTransport.socket = conn.socket;
                nakamaTransport.matchId = connection.serverAddress;
            }
            else if (!string.IsNullOrEmpty(_transportRoomField))
            {
                ApplyRoomField(transport, _transportRoomField, connection.serverAddress);
            }

            if (shouldBeHost)
                manager.StartHost();
            else
                manager.StartClient();
        }

        private static void ApplyRoomField(Component transport, string fieldName, string value)
        {
            if (transport == null || string.IsNullOrEmpty(fieldName))
                return;

            var type = transport.GetType();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var prop = type.GetProperty(fieldName, flags);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(transport, value);
                return;
            }

            var field = type.GetField(fieldName, flags);
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(transport, value);
                return;
            }

            PurrLogger.LogWarning($"Transport `{type.Name}` has no string field/property `{fieldName}`. Skipping room assignment.");
        }
    }
}
#endif
