using System;
using System.Threading.Tasks;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby
{
    public struct GameStartResponse
    {
        public bool success;
        public string error;
        public ConnectionInfo connection;

        public static GameStartResponse Success(ConnectionInfo connection) =>
            new GameStartResponse { success = true, connection = connection };
        public static GameStartResponse Failure(string error) =>
            new GameStartResponse { success = false, error = error };

        public override string ToString()
        {
            return $"success: {success}, error: {error}, connection: {{ {connection} }}";
        }
    }

    public abstract class GameAllocatorProvider : ScriptableObject
    {
        /// <summary>Called once after the session provider has logged in.</summary>
        public virtual Task Initialize() => Task.CompletedTask;

        /// <summary>Provider-local cleanup.</summary>
        public virtual Task Logout() => Task.CompletedTask;

        public abstract Task<GameStartResponse> AllocateGame(ILobby lobby);

        public abstract Task LoadGame(ILobby lobby);

        /// <summary>Allocates a game from a matchmaking result. If the result has a lobby, delegates to the lobby overload; otherwise returns the pre-populated connection info.</summary>
        public virtual Task<GameStartResponse> AllocateGame(MatchResult matchResult)
        {
            if (matchResult.lobby != null)
                return AllocateGame(matchResult.lobby);
            return Task.FromResult(GameStartResponse.Success(matchResult.connection));
        }

        /// <summary>Loads the game scene from a matchmaking result. Delegates to the lobby overload by default.</summary>
        public virtual Task LoadGame(MatchResult matchResult)
        {
            return LoadGame(matchResult.lobby);
        }

        /// <summary>
        /// False for allocators that connect to a dedicated server (e.g. Edgegap):
        /// <see cref="Connect"/> then always starts a client, never a host.
        /// </summary>
        protected virtual bool supportsHosting => true;

        /// <summary>
        /// Configures the transport via <see cref="ConfigureTransport"/> and starts
        /// the host or client on <see cref="NetworkManager.main"/>.
        /// </summary>
        public void Connect(ConnectionInfo connection, bool shouldBeHost)
        {
            var networkManager = NetworkManager.main;
            if (!networkManager)
            {
                PurrLogger.LogError("No `NetworkManager` found in the scene.");
                return;
            }

            if (networkManager.shouldAutoStartServer || networkManager.shouldAutoStartClient)
            {
                PurrLogger.LogError("`NetworkManager` is set to auto start (has auto start flags). Please disable auto start and try again.");
                return;
            }

            if (shouldBeHost && !supportsHosting)
            {
                PurrLogger.LogWarning($"`{name}` does not support hosting; connecting as client instead.");
                shouldBeHost = false;
            }

            if (!ConfigureTransport(networkManager, connection, shouldBeHost))
                return;

            if (shouldBeHost)
                 networkManager.StartHost();
            else networkManager.StartClient();
        }

        /// <summary>
        /// Configure (and if needed add and assign) the transport on the manager.
        /// Return false to abort the connection attempt (log the reason).
        /// </summary>
        protected abstract bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost);

        /// <summary>
        /// Loads the game scene and captures the menu scene to return to. Concrete allocators call this
        /// from their <see cref="LoadGame(ILobby)"/> instead of loading the scene themselves.
        /// </summary>
        protected Task LoadGameScene(string gameScene)
        {
            if (string.IsNullOrEmpty(gameScene))
                throw new Exception($"Game scene is not set. Please set the game scene in the inspector of `{name}`.");

            var orch = GameOrchestrator.active;
            if (orch && string.IsNullOrEmpty(orch.menuScene))
                orch.menuScene = SceneManager.GetActiveScene().name;

            var asyncOp = SceneManager.LoadSceneAsync(gameScene);
            if (asyncOp == null)
            {
                PurrLogger.LogError($"Loading scene `{gameScene}` failed.");
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            asyncOp.completed += _ =>
            {
                GameSession.EnsureInScene(SceneManager.GetActiveScene());
                tcs.SetResult(true);
            };
            return tcs.Task;
        }

        /// <summary>Gets or adds a component on the given GameObject.</summary>
        protected static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
        }
    }
}
